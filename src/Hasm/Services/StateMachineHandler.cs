using System.Text;
using Hasm.Models;
using Jint.Native;

namespace Hasm.Services;

public class StateMachineHandler(StateMachine _stateMachine, ClientService _clientService, DataService _dataService, VariableService _variableService, MessageBusService _messageBusService) : IDisposable
{
    public enum StateMachineRunningState
    {
        NotRunning,
        Running,
        Finished,
        Error
    }

    public class LogEntry
    {
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public int StateMachineId { get; set; }
        public string Message { get; set; } = null!;
    }

    public class StateMachineHandlerInfo
    {
        public int StateMachineId { get; set; }
        public string? CurrentState { get; set; }
        public StateMachineRunningState? RunningState { get; set; }
    }

    public class StateMachineEngineInfo
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public StateMachine StateMachine { get; set; } = null!;
        public Jint.Engine Engine { get; set; } = null!;
    }

    public StateMachine StateMachine { get; private set; } = _stateMachine;
    public string? ErrorMessage { get; private set; }

    public void SetRunningStateFinished(string instanceId)
    {
        var indexOfEngine = _engines.FindIndex(_engines => _engines.Id.ToString() == instanceId);
        if (indexOfEngine < 0)
        {
            return;
        }
        if (indexOfEngine == 0)
        {
            RunningState = StateMachineRunningState.Finished;
        }
        else
        {
            //get the parameter mappings
            //we need the current state of the parent state machine
            var parentEngine = _engines[indexOfEngine - 1];
            var parentStateId = parentEngine.Engine.Evaluate("stateInfo[currentState].externalId").JsValueToString();
            var parentState = parentEngine.StateMachine.States.FirstOrDefault(s => s.Id.ToString() == parentStateId);
            if (parentState != null)
            {
                foreach (var parameter in _engines[indexOfEngine].StateMachine.SubStateMachineParameters.Where(x => x.IsOutput).ToList())
                {
                    var subScriptVariableName = parentState.SubStateParameters.FirstOrDefault(x => x.Id == parameter.Id)?.ScriptVariableName;
                    var parentScriptVariableName = parameter.ScriptVariableName;
                    if (!string.IsNullOrWhiteSpace(subScriptVariableName) && !string.IsNullOrWhiteSpace(parentScriptVariableName))
                    {
                        var jsValue = _engines[indexOfEngine].Engine.Evaluate(parentScriptVariableName);
                        var srcVariableValue = jsValue.JsValueToString(true);
                        parentEngine.Engine.Evaluate($"{subScriptVariableName} = {srcVariableValue}");
                    }
                }
            }
        }
        while (_engines.Count > indexOfEngine && _engines.Count > 1)
        {
            _engines[_engines.Count - 1].Engine.Dispose();
            _engines.RemoveAt(_engines.Count - 1);
        }
        RequestTriggerStateMachine();
    }

    public void StartSubStateMachine(string stateId, string instanceId)
    {
        var indexOfEngine = _engines.FindIndex(_engines => _engines.Id.ToString() == instanceId);
        if (indexOfEngine < 0)
        {
            return;
        }

        var stateState = _engines[indexOfEngine].StateMachine.States.Where(s => s.Id.ToString() == stateId).FirstOrDefault();
        if (stateState == null)
        {
            return;
        }

        var stateMachineId = stateState.SubStateMachineId;
        if (stateMachineId == null)
        {
            return;
        }

        if (_engines.Where(x => x.StateMachine.Id == stateMachineId).Any())
        {
            return;
        }

        var subStateMachine = _dataService.GetStateMachines().FirstOrDefault(x => x.Id == stateMachineId);
        if (subStateMachine == null)
        {
            return;
        }

        var engine = new StateMachineEngineInfo()
        {
            Engine = new Jint.Engine(),
            StateMachine = subStateMachine
        };
        _engines.Add(engine);
        engine.Engine.SetValue("system", _systemMethods);

        List<(string variableName, string? variableValue)> machineStateParameters = [];
        foreach(var parameter in subStateMachine.SubStateMachineParameters)
        {
            if (parameter.IsInput)
            {
                var scriptVariableName = stateState.SubStateParameters.FirstOrDefault(x => x.Id == parameter.Id)?.ScriptVariableName;
                var jsValue = scriptVariableName == null ? null : _engines[indexOfEngine].Engine.Evaluate(scriptVariableName);
                var srcVariableValue = jsValue.JsValueToString(true);
                machineStateParameters.Add((variableName: parameter.ScriptVariableName, variableValue: srcVariableValue));
            }
            else
            {
                machineStateParameters.Add((variableName: parameter.ScriptVariableName, variableValue: string.IsNullOrWhiteSpace(parameter.DefaultValue) ? "null" : parameter.DefaultValue));
            }
        }

        engine.Engine.Execute(EngineScriptBuilder.BuildEngineScript(subStateMachine, false, engine.Id, machineStateParameters));
        RequestTriggerStateMachine();
    }

    public bool IsSubStateMachineRunning(string instanceId)
    {
        var indexOfEngine = _engines.FindIndex(_engines => _engines.Id.ToString() == instanceId);
        if (indexOfEngine < 0)
        {
            return false;
        }
        return indexOfEngine < (_engines.Count - 1);
    }

    private StateMachineRunningState _runningState = StateMachineRunningState.NotRunning;
    public StateMachineRunningState RunningState
    {
        get => _runningState;
        set
        {
            if (_runningState != value)
            {
                _runningState = value;
                PublishStateMachineHandlerInfo();
            }
        }
    }

    private string? _currentState = null;
    public string? CurrentState
    {
        get => _currentState;
        set
        {
            if (_currentState != value)
            {
                _currentState = value;
                RequestTriggerStateMachine();
                PublishStateMachineHandlerInfo();
            }
        }
    }

    private readonly object _lockEngineObject = new object();
    private List<StateMachineEngineInfo> _engines = [];
    private SystemMethods? _systemMethods = null;
    private bool _readyForTriggers = false;


    public static string SystemScript => SystemMethods.SystemScript;

    private static System.Text.Json.JsonSerializerOptions logJsonOptions = new System.Text.Json.JsonSerializerOptions
    {
        WriteIndented = true,
        IncludeFields = true
    };

    public void Dispose()
    {
        Stop();
    }

    private void DisposeEngines()
    {
        foreach (var engine in _engines)
        {
            engine.Engine.Dispose();
        }
        _engines.Clear();
    }

    public void Restart()
    {
        ErrorMessage = null;
        Stop();
        Start();
    }

    public void Start()
    {
        _readyForTriggers = false;
        if (StateMachine.Enabled || StateMachine.IsSubStateMachine)
        {
            lock (_lockEngineObject)
            {
                _currentState = null;
                if (EngineScriptBuilder.ValidateModel(StateMachine))
                {
                    var engine = new StateMachineEngineInfo()
                    {
                        Engine = new Jint.Engine(),
                        StateMachine = StateMachine
                    };
                    _engines.Add(engine);
                    _systemMethods = new SystemMethods(_clientService, _dataService, _variableService, this);
                    engine.Engine.SetValue("system", _systemMethods);

                    try
                    {
                        engine.Engine.Execute(EngineScriptBuilder.BuildEngineScript(StateMachine, true, engine.Id, null));
                        RunningState = StateMachineRunningState.Running;
                        RequestTriggerStateMachine();
                    }
                    catch (Exception e)
                    {
                        DisposeEngines();
                        ErrorMessage = $"Error initializing statemachine: {e.Message}";
                        RunningState = StateMachineRunningState.Error;
                    }
                }
                else
                {
                    ErrorMessage = $"Statemachine is not valid";
                    RunningState = StateMachineRunningState.Error;
                }
            }
            _readyForTriggers = true;
        }
        else
        {
            RunningState = StateMachineRunningState.NotRunning;
        }
    }

    public void Stop()
    {
        _readyForTriggers = false;
        lock (_lockEngineObject)
        {
            RunningState = StateMachineRunningState.NotRunning;
            DisposeEngines();
        }
    }

    public Task Handle(List<VariableService.VariableInfo> variableInfos)
    {
        RequestTriggerStateMachine();
        return Task.CompletedTask;
    }

    public Task Handle(List<VariableService.VariableValueInfo> variableValueInfos)
    {
        RequestTriggerStateMachine();
        return Task.CompletedTask;
    }

    private long _triggering = 0;
    private readonly object _triggerLock = new object();
    private void RequestTriggerStateMachine()
    {
        var count = Interlocked.Read(ref _triggering);
        if (count < 3)
        {
            Interlocked.Increment(ref _triggering);
            Task.Factory.StartNew(() =>
            {
                lock (_triggerLock)
                {
                    TriggerProcess();
                }
                Interlocked.Decrement(ref _triggering);
            });
        }
    }

    public void TriggerProcess()
    {
        if (!_readyForTriggers) return;

        lock (_lockEngineObject)
        {
            if (RunningState == StateMachineRunningState.Running && _engines.Any())
            {
                try
                {
                    var index = 0;
                    while (index < _engines.Count)
                    {
                        _engines[index].Engine.Invoke("schedule");
                        index++;
                    }
                }
                catch (Exception e)
                {
                    ErrorMessage = $"Error in script{e.Message}";
                    RunningState = StateMachineRunningState.Error;
                }
            }
        }
    }

    public Task UpdateAsync(StateMachine stateMachine)
    {
        Stop();
        StateMachine = stateMachine;
        if (!StateMachine.IsSubStateMachine)
        {
            Start();
        }
        return Task.CompletedTask;
    }

    public string? ExecuteScript(string script)
    {
        string? result = null;
        if (_engines.Any())
        {
            var autoResetEvent = new AutoResetEvent(false);
            Task.Run(() =>
            {
                lock (_lockEngineObject)
                {
                    try
                    {
                        var jsValue = _engines[0].Engine.Evaluate(script);
                        result = jsValue.JsValueToString();
                    }
                    catch (Exception e)
                    {
                        result = $"Error: {e.Message}";
                    }
                }
                autoResetEvent.Set();
            });
            autoResetEvent.WaitOne();
            autoResetEvent.Dispose();
        }
        return result;
    }

    public async Task AddLogAsync(string instanceId, object? logObject)
    {
        if (logObject != null)
        {
            var indexOfEngine = _engines.FindIndex(_engines => _engines.Id.ToString() == instanceId);
            var prefix = $"[{string.Join("].[", _engines.Take(indexOfEngine+1).Select(e => e.StateMachine.Name))}]";
            var logEvent = new LogEntry
            {
                Timestamp = DateTime.UtcNow,
                StateMachineId = StateMachine.Id,
            };

            if (logObject is string txt)
            {
                logEvent.Message = txt;
            }
            else
            {
                logEvent.Message = System.Text.Json.JsonSerializer.Serialize(logObject, logJsonOptions);
            }
            logEvent.Message = $"{prefix}: {logEvent.Message}";

            await _messageBusService.PublishAsync(logEvent);
        }
    }

    public void PublishStateMachineHandlerInfo()
    {
        var info = new StateMachineHandlerInfo
        {
            StateMachineId = StateMachine.Id,
            CurrentState = CurrentState,
            RunningState = RunningState
        };
        _messageBusService.PublishAsync(info);
    }
}
