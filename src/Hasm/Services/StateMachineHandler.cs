using Hasm.Models;
using System.Text;

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

    public StateMachine StateMachine { get; private set; } = _stateMachine;
    public string? ErrorMessage { get; private set; }

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
    private Jint.Engine? _engine = null;
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

    private void DisposeEngine()
    {
        _engine?.Dispose();
        _engine = null;
    }

    public void Restart()
    {
        Stop();
        Start();
    }

    public void Start()
    {
        _readyForTriggers = false;
        if (StateMachine.Enabled)
        {
            lock (_lockEngineObject)
            {
                _currentState = null;
                if (ValidateModel(StateMachine))
                {
                    _engine = new Jint.Engine();
                    _systemMethods = new SystemMethods(_clientService, _dataService, _variableService, this);
                    _engine.SetValue("system", _systemMethods);

                    try
                    {
                        _engine.Execute(BuildEngineScript(StateMachine));
                        RunningState = StateMachineRunningState.Running;
                        RequestTriggerStateMachine();
                    }
                    catch (Exception e)
                    {
                        DisposeEngine();
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
            DisposeEngine();
        }
    }

    public Task Handle(List<VariableService.VariableInfo> variableInfos)
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
            if (RunningState == StateMachineRunningState.Running && _engine != null)
            {
                try
                {
                    _engine.Invoke("schedule");
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
        StateMachine = stateMachine;
        return Task.CompletedTask;
    }

    private bool ValidateModel(StateMachine stateMachine)
    {
        //do we have one start state?
        if (GetStartState(stateMachine) == null)
        {
            return false;
        }

        //one rror state
        var states = stateMachine.States.Where(x => x.IsErrorState).ToList();
        if (states.Count > 1)
        {
            return false;
        }

        return true;
    }

    private State? GetStartState(StateMachine stateMachine)
    {
        var states = stateMachine.States.Where(x => x.IsStartState).ToList();
        if (states.Count == 1)
        {
            return states[0];
        }
        else if (states.Count > 1)
        {
            return null;
        }

        foreach (var state in stateMachine.States)
        {
            if (!stateMachine.Transitions.Any(x => x.ToStateId == state.Id))
            {
                states.Add(state);
            }
        }

        if (states.Count == 1)
        {
            return states[0];
        }

        return null;
    }

    public string? ExecuteScript(string script)
    {
        string? result = null;
        if (_engine != null)
        {
            var autoResetEvent = new AutoResetEvent(false);
            Task.Run(() =>
            {
                lock (_lockEngineObject)
                {
                    try
                    {
                        var jsValue = _engine.Evaluate(script);
                        if (jsValue == null)
                        {
                            result = "null";
                        }
                        else
                        {
                            var obj = jsValue.ToObject();
                            if (obj == null)
                            {
                                result = "null";
                            }
                            else if (obj is string s)
                            {
                                result = s;
                            }
                            else if (obj.GetType().IsValueType)
                            {
                                result = obj.ToString();
                            }
                            else
                            {
                                result = System.Text.Json.JsonSerializer.Serialize(obj, logJsonOptions);
                            }
                        }
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

    public string BuildEngineScript(StateMachine stateMachine)
    {
        var script = new StringBuilder();

        script.AppendLine(SystemScript);

        foreach (var state in stateMachine.States)
        {
            script.AppendLine();
            script.AppendLine($"//State Entry [{state.Name}]");
            script.AppendLine($"function {StateEntryMethodName(state)}() {{ ");
            if (state.EntryAction != null)
            {
                var allLines = state.EntryAction.Split(new char[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in allLines)
                {
                    script.AppendLine($"  {line}");
                }
            }
            script.AppendLine($"}}");
        }

        foreach (var transition in stateMachine.Transitions)
        {
            script.AppendLine();
            var fromState = stateMachine.States.First(s => s.Id == transition.FromStateId);
            var toState = stateMachine.States.First(s => s.Id == transition.ToStateId);
            script.AppendLine($"//Transition from [{fromState.Name}] to [{toState.Name}]");
            script.AppendLine($"function {TransitionMethodName(transition)}() {{ ");
            script.AppendLine($"  return {transition.Condition ?? "true"} ;");
            script.AppendLine($"}}");
        }

        script.AppendLine();
        script.AppendLine($"//Pre schedule action");
        script.AppendLine($"function preScheduleAction() {{ ");
        if (stateMachine.PreScheduleAction != null)
        {
            var allLines = stateMachine.PreScheduleAction.Split(new char[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in allLines)
            {
                script.AppendLine($"  {line}");
            }
        }
        script.AppendLine($"}}");

        script.AppendLine();
        script.AppendLine("var stateInfo = []");
        foreach (var state in stateMachine.States)
        {
            script.AppendLine($"stateInfo['{state.Id.ToString("N")}'] = {{");
            script.AppendLine($"'name': '{state.Name.Replace('\'', ' ')}',");
            script.AppendLine($"'externalId': '{state.Id.ToString()}'");
            script.AppendLine($"}}");
        }
        script.AppendLine("var currentState = null");
        script.AppendLine($"var startState = '{GetStartState(stateMachine)?.Id.ToString("N")}'");

        script.AppendLine("var stateTransitionMap = []");
        foreach (var transition in stateMachine.Transitions)
        {
            var fromState = stateMachine.States.First(s => s.Id == transition.FromStateId);
            var toState = stateMachine.States.First(s => s.Id == transition.ToStateId);
            script.AppendLine($"stateTransitionMap.push({{'fromState': '{fromState.Id.ToString("N")}', 'transition': '{transition.Id.ToString("N")}', 'toState': '{toState.Id.ToString("N")}'}})");
        }

        script.AppendLine(""""
            function schedule() {
            	preScheduleAction()
            	if (currentState == null)
            	{
            		changeState(startState)
            	}
            	else
            	{
            	    var transitions = stateTransitionMap.filter((transition) => transition.fromState == currentState)
                    if (transitions.length == 0)
                    {
                       log('No transitions found for current state: ' + stateInfo[currentState].name)
                       system.setRunningStateToFinished()
                    }
            		var successFulTransition = transitions.find((transition) => eval('transitionResult'+transition.transition+'()'))
            		if (successFulTransition != null)
            		{
            		    changeState(successFulTransition.toState)
            		}
            	}
            }

            function changeState(state) {
                log('changing state to: ' + stateInfo[state].name)
            	eval('stateEntryAction'+state+'()')
            	currentState = state
                system.setCurrentState(stateInfo[state].name)
            }
            
            """");

        script.AppendLine();
        script.AppendLine("// Pre-start statemachine action");
        script.AppendLine($"{stateMachine.PreStartAction ?? ""}");

        return script.ToString();
    }

    private static string TransitionMethodName(Transition transition)
    {
        return $"transitionResult{transition.Id.ToString("N")}";
    }

    private static string StateEntryMethodName(State state)
    {
        return $"stateEntryAction{state.Id.ToString("N")}";
    }

    public async Task AddLogAsync(object? logObject)
    {
        if (logObject != null)
        {
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
