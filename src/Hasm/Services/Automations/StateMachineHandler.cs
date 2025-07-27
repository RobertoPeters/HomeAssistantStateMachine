using System.Text;
using System.Text.Json.Serialization;
using Hasm.Models;
using Hasm.Services.Interfaces;
using Jint.Native;

namespace Hasm.Services.Automations;

public class StateMachineHandler : IAutomationHandler
{
    public class State
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = null!;
        public bool IsErrorState { get; set; }
        public bool IsStartState { get; set; }
        public bool IsSubState { get; set; }
        public string? Description { get; set; }
        public string? EntryAction { get; set; }
        public string? UIData { get; set; }
        public int? SubStateMachineId { get; set; }
        public List<SubStateParameter> SubStateParameters { get; set; } = [];
    }

    public class SubStateMachineParameter
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = null!;
        public string ScriptVariableName { get; set; } = null!;
        public string? DefaultValue { get; set; }
        public bool IsOutput { get; set; }
        public bool IsInput { get; set; }
    }

    public class SubStateParameter
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string? ScriptVariableName { get; set; }
    }

    public class Transition
    {
        public Guid Id { get; set; }
        public string? Description { get; set; }
        public string? Condition { get; set; }
        public string? UIData { get; set; }
        public Guid? FromStateId { get; set; }
        public Guid? ToStateId { get; set; }
    }

    public class Information
    {
        public Guid Id { get; set; }
        public string? Description { get; set; }
        public string? Evaluation { get; set; }
        public string? UIData { get; set; }

        [JsonIgnore]
        public string? EvaluationResult { get; set; }
    }

    public class AutomationProperties
    {
        public string? PreStartAction { get; set; }
        public string? PreScheduleAction { get; set; }
        public List<State> States { get; set; } = [];
        public List<Transition> Transitions { get; set; } = [];
        public List<SubStateMachineParameter> SubStateMachineParameters { get; set; } = [];
        public List<Information> Informations { get; set; } = [];
    }

    public enum StateMachineRunningState
    {
        NotRunning,
        Running,
        Finished,
        Error
    }

    public class StateMachineHandlerInfo
    {
        public int AutomationId { get; set; }
        public string? CurrentState { get; set; }
        public StateMachineRunningState? RunningState { get; set; }
    }

    public class StateMachineEngineInfo
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Automation Automation { get; set; } = null!;
        public Jint.Engine Engine { get; set; } = null!;
    }

    private Automation _automation;
    private readonly ClientService _clientService;
    private readonly DataService _dataService;
    private readonly VariableService _variableService;
    private readonly MessageBusService _messageBusService;

    private AutomationProperties _automationProperties = new();

    public Automation Automation => _automation;
    public string? ErrorMessage { get; private set; }

    public string? PreStartAction
    {
        get => _automationProperties.PreStartAction;
        set { _automationProperties.PreStartAction = value; }
    }
    public string? PreScheduleAction
    {
        get => _automationProperties.PreScheduleAction;
        set { _automationProperties.PreScheduleAction = value; }
    }
    public List<State> States
    {
        get => _automationProperties.States;
        set { _automationProperties.States = value; }
    }
    public List<Transition> Transitions
    {
        get => _automationProperties.Transitions;
        set { _automationProperties.Transitions = value; }
    }
    public List<SubStateMachineParameter> SubStateMachineParameters
    {
        get => _automationProperties.SubStateMachineParameters;
        set { _automationProperties.SubStateMachineParameters = value; }
    }
    public List<Information> Informations
    {
        get => _automationProperties.Informations;
        set { _automationProperties.Informations = value; }
    }

    public StateMachineHandler(Automation automation, ClientService clientService, DataService dataService, VariableService variableService, MessageBusService messageBusService)
    {
        _automation = automation;
        _clientService = clientService;
        _dataService = dataService;
        _variableService = variableService;
        _messageBusService = messageBusService;

        _automationProperties = GetAutomationProperties(automation.Data);
    }

    public static AutomationProperties GetAutomationProperties(string? data)
    {
        if (!string.IsNullOrWhiteSpace(data))
        {
            return System.Text.Json.JsonSerializer.Deserialize<AutomationProperties>(data) ?? new();
        }
        return new();
    }

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
            var parentState = GetAutomationProperties(parentEngine.Automation.Data).States.FirstOrDefault(s => s.Id.ToString() == parentStateId);
            if (parentState != null)
            {
                foreach (var parameter in GetAutomationProperties(_engines[indexOfEngine].Automation.Data).SubStateMachineParameters.Where(x => x.IsOutput).ToList())
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

        var stateState = GetAutomationProperties(_engines[indexOfEngine].Automation.Data).States.Where(s => s.Id.ToString() == stateId).FirstOrDefault();
        if (stateState == null)
        {
            return;
        }

        var automationId = stateState.SubStateMachineId;
        if (automationId == null)
        {
            return;
        }

        if (_engines.Where(x => x.Automation.Id == automationId).Any())
        {
            return;
        }

        var subStateMachine = _dataService.GetAutomations().FirstOrDefault(x => x.Id == automationId);
        if (subStateMachine == null)
        {
            return;
        }

        var engine = new StateMachineEngineInfo()
        {
            Engine = new Jint.Engine(),
            Automation = subStateMachine
        };
        _engines.Add(engine);
        engine.Engine.SetValue("system", _systemMethods);

        List<(string variableName, string? variableValue)> machineStateParameters = [];
        var subStateMachineParameters = GetAutomationProperties(subStateMachine.Data).SubStateMachineParameters;
        foreach (var parameter in subStateMachineParameters)
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

        engine.Engine.Execute(EngineScriptBuilder.BuildEngineScript(GetAutomationProperties(subStateMachine.Data), false, engine.Id, machineStateParameters));
        RequestTriggerStateMachine();
    }

    public bool IsSubStateMachineRunning(string instanceId)
    {
        var indexOfEngine = _engines.FindIndex(_engines => _engines.Id.ToString() == instanceId);
        if (indexOfEngine < 0)
        {
            return false;
        }
        return indexOfEngine < _engines.Count - 1;
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
        Stop();
        Start();
    }

    public void Start()
    {
        ErrorMessage = null;
        _readyForTriggers = false;
        if (Automation.Enabled || Automation.IsSubAutomation)
        {
            lock (_lockEngineObject)
            {
                _currentState = null;
                if (EngineScriptBuilder.ValidateModel(_automationProperties))
                {
                    var engine = new StateMachineEngineInfo()
                    {
                        Engine = new Jint.Engine(),
                        Automation = Automation
                    };
                    _engines.Add(engine);
                    _systemMethods = new SystemMethods(_clientService, _dataService, _variableService, this);
                    engine.Engine.SetValue("system", _systemMethods);

                    try
                    {
                        engine.Engine.Execute(EngineScriptBuilder.BuildEngineScript(_automationProperties, true, engine.Id, null));
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

        _messageBusService.PublishAsync(new AutomationInfo
        {
            AutomationId = Automation.Id
        });
    }

    public Task UpdateAsync(Automation automation)
    {
        Stop();
        _automation = automation;
        _automationProperties = GetAutomationProperties(automation.Data);
        if (!_automation.IsSubAutomation)
        {
            Start();
        }
        return Task.CompletedTask;
    }

    public void ExecuteScript(List<Information> informations)
    {
        if (_engines.Any())
        {
            var autoResetEvent = new AutoResetEvent(false);
            Task.Run(() =>
            {
                lock (_lockEngineObject)
                {
                    foreach (var information in informations)
                    {
                        try
                        {
                            var jsValue = _engines[0].Engine.Evaluate(information.Evaluation!);
                            information.EvaluationResult = jsValue.JsValueToString();
                        }
                        catch (Exception e)
                        {
                            information.EvaluationResult = $"Error: {e.Message}";
                        }
                    }
                }
                autoResetEvent.Set();
            });
            autoResetEvent.WaitOne();
            autoResetEvent.Dispose();
        }
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
            var prefix = $"[{string.Join("].[", _engines.Take(indexOfEngine+1).Select(e => e.Automation.Name))}]";
            var logEvent = new LogEntry
            {
                Timestamp = DateTime.UtcNow,
                AutomationId = Automation.Id,
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
            AutomationId = Automation.Id,
            CurrentState = CurrentState,
            RunningState = RunningState
        };
        _messageBusService.PublishAsync(info);
    }
}
