using HomeAssistantStateMachine.Models;
using System.Text;

namespace HomeAssistantStateMachine.Services;

public partial class StateMachineHandler : IDisposable
{
    public enum StateMachineRunningState
    {
        NotRunning,
        Running,
        Error
    }

    private sealed class HAStateChangedCallBackInfo
    {
        public HAStateChangedCallBackInfo(int variableId, Func<Jint.Native.JsValue, Jint.Native.JsValue[], Jint.Native.JsValue> callback)
        {
            VariableId = variableId;
            Callback = callback;
        }
        public Func<Jint.Native.JsValue, Jint.Native.JsValue[], Jint.Native.JsValue> Callback { get; private set; }
        public int VariableId { get; private set; }
        public bool Triggered { get; private set; }
        private object? _data;
        public object? Data
        {
            get { return _data; }
            set
            {
                _data = value;
                Triggered = true;
            }
        }

        public void ExecuteCallback(Jint.Engine engine)
        {
            Triggered = false;
            Callback.Invoke(Jint.Native.JsValue.Undefined, [Jint.Native.JsValue.FromObject(engine, Data)]);
        }
    }

    public StateMachine StateMachine { get; private set; }
    public string? ErrorMessage { get; private set; }

    private StateMachineRunningState _runningState = StateMachineRunningState.NotRunning;
    public StateMachineRunningState RunningState
    {
        get => _runningState;
        private set
        {
            if (_runningState != value)
            {
                if (value == StateMachineRunningState.Error && _runningState == StateMachineRunningState.Running)
                {
                    var errorState = StateMachine.States.FirstOrDefault(x => x.IsErrorState);
                    if (errorState != null && CurrentState?.Id != errorState.Id)
                    {
                        ChangeToState(errorState);
                    }
                    else
                    {
                        _runningState = value;
                    }
                }
                else
                {
                    _runningState = value;
                    if (value != StateMachineRunningState.Error)
                    {
                        ErrorMessage = "";
                    }
                }
            }
        }
    }

    private readonly object _lockObject = new object();
    private Jint.Engine? _engine = null;
    private readonly List<HAStateChangedCallBackInfo> _haStateChangedCallBackInfos = [];
    private volatile bool _readyForTriggers = false;
    private readonly VariableService _variableService;
    private readonly HAClientService _haClientService;

    private State? _engineRequestToState = null;
    private State? _currentState = null;
    public State? CurrentState
    {
        get => _currentState;
        private set
        {
            if (value?.Id != _currentState?.Id)
            {
                _currentState = value;
                StateChanged?.Invoke(this, value);
            }
        }
    }

    public event EventHandler<State?>? StateChanged;
    public event EventHandler<string>? Log;

    public StateMachineHandler(StateMachine stateMachine, VariableService variableService, HAClientService haClientService)
    {
        _lockObject = new object();
        StateMachine = stateMachine;
        _variableService = variableService;
        _haClientService = haClientService;
    }

    public bool SetHAStateChanged(HAClientHandler clientHandler, Variable variable, Func<Jint.Native.JsValue, Jint.Native.JsValue[], Jint.Native.JsValue> callback)
    {
        //call from engine, so no lock required (already locked)
        var existing = _haStateChangedCallBackInfos.Find(x => x.VariableId == variable.Id);
        if (existing != null)
        {
            _haStateChangedCallBackInfos.Remove(existing);
        }
        var cbInfo = new HAStateChangedCallBackInfo(variable.Id, callback);
        if (clientHandler.SetStateChangedCallback(StateMachine.Id, variable, (data) =>
            {
                cbInfo.Data = data;
                Task.Factory.StartNew(() =>
                {
                    TriggerProcess();
                });
            }))
        {
            _haStateChangedCallBackInfos.Add(cbInfo);
            return true;
        }
        return false;
    }

    public string? ExecuteScript(string script)
    {
        string? result = null;
        if (_engine != null)
        {
            var autoResetEvent = new AutoResetEvent(false);
            Task.Run(() =>
            {
                lock (_lockObject)
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

    public void SetEngineRequestToState(string? stateName)
    {
        _engineRequestToState = StateMachine.States.FirstOrDefault(x => x.Name == stateName);
    }

    public string? GetEngineState()
    {
        return CurrentState?.Name;
    }

    static System.Text.Json.JsonSerializerOptions logJsonOptions = new System.Text.Json.JsonSerializerOptions
    {
        WriteIndented = true,
        IncludeFields = true
    };
    public void AddLog(object? logObject)
    {
        if (Log != null && logObject != null)
        {
            if (logObject is string txt)
            {
                Log.Invoke(this, txt);
            }
            else
            {
                var txt2 = System.Text.Json.JsonSerializer.Serialize(logObject, logJsonOptions);
                Log.Invoke(this, txt2);
            }
        }
    }

    public void Dispose()
    {
        _readyForTriggers = false;
        lock (_lockObject)
        {
            RunningState = StateMachineRunningState.NotRunning;
            DisposeEngine();
        }
    }

    private void DisposeEngine()
    {
        _haStateChangedCallBackInfos.Clear();
        _haClientService.GetClients().ForEach(x => x.RemoveRegistrarFromStateChangedCallback(StateMachine.Id));
        _engine?.Dispose();
        _engine = null;
    }

    private State? GetStartState()
    {
        var states = StateMachine.States.Where(x => x.IsStartState).ToList();
        if (states.Count == 1)
        {
            return states[0];
        }
        else if (states.Count > 1)
        {
            return null;
        }

        foreach (var state in StateMachine.States)
        {
            if (!StateMachine.Transitions.Any(x => x.ToStateId == state.Id))
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

    private bool ValidateModel()
    {
        //do we have one start state?
        if (GetStartState() == null)
        {
            return false;
        }

        //one rror state
        var states = StateMachine.States.Where(x => x.IsErrorState).ToList();
        if (states.Count > 1)
        {
            return false;
        }

        return true;
    }

    public string BuildEngineScript(StateMachine stateMachine)
    {
        var script = new StringBuilder();

        script.AppendLine(SystemScript);

        foreach (var state in stateMachine.States)
        {
            script.AppendLine();
            script.AppendLine($"//State Entry [{state.Name}]");
            script.AppendLine($"function stateEntryAction{state.Id}() {{ ");
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
            script.AppendLine($"function transitionResult{transition.Id}() {{ ");
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
        script.AppendLine("// Pre-start statemachine action");
        script.AppendLine($"{stateMachine.PreStartAction ?? ""}");

        return script.ToString();
    }

    public void Start()
    {
        _readyForTriggers = false;
        if (StateMachine.Enabled)
        {
            lock (_lockObject)
            {
                _currentState = null;
                _engineRequestToState = null;
                if (ValidateModel())
                {
                    _engine = new Jint.Engine();
                    _engine.SetValue("system", NewSystemMethods);

                    try
                    {
                        _engine.Execute(BuildEngineScript(StateMachine));
                        RunningState = StateMachineRunningState.Running;
                        _engine.Invoke("preScheduleAction");
                        ChangeToState(_engineRequestToState ?? GetStartState());
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
        lock (_lockObject)
        {
            RunningState = StateMachineRunningState.NotRunning;
            DisposeEngine();
            ChangeToState(null);
        }
    }

    public void TriggerProcess()
    {
        if (!_readyForTriggers) return;

        lock (_lockObject)
        {

            if (RunningState == StateMachineRunningState.Running && CurrentState != null && _engine != null)
            {
                Transition? activeTransition = null;
                try
                {
                    foreach (var cbInfo in _haStateChangedCallBackInfos)
                    {
                        if (cbInfo.Triggered)
                        {
                            cbInfo.ExecuteCallback(_engine);
                        }
                    }

                    _engine.Invoke("preScheduleAction");
                    var transitions = StateMachine.Transitions
                        .Where(t => t.FromStateId == CurrentState.Id)
                        .ToList();
                    if (_engineRequestToState != null)
                    {
                        var state = _engineRequestToState;
                        _engineRequestToState = null;
                        ChangeToState(state);
                    }
                    else
                    {
                        foreach (var transition in transitions)
                        {
                            activeTransition = transition;
                            var condition = _engine.Evaluate($"transitionResult{transition.Id}()").ToObject();
                            if ((bool)condition!)
                            {
                                ChangeToState(StateMachine.States.First(s => s.Id == transition.ToStateId));
                                break;
                            }
                        }
                    }

                    foreach (var cbInfo in _haStateChangedCallBackInfos)
                    {
                        if (cbInfo.Triggered)
                        {
                            cbInfo.ExecuteCallback(_engine);
                        }
                    }
                }
                catch (Exception e)
                {
                    ErrorMessage = $"Error in transition ({activeTransition?.Description ?? activeTransition?.Condition}): {e.Message}";
                    RunningState = StateMachineRunningState.Error;
                }
            }
        }
    }

    public void UpdateStateMachine(StateMachine stateMachine)
    {
        Stop();
        StateMachine = stateMachine;
        Start();
    }

    private void ChangeToState(State? state)
    {
        AddLog($"Changing state to {state?.Name ?? "null"}");
        CurrentState = state;
        if (RunningState == StateMachineRunningState.Running && _engine != null && state != null)
        {
            try
            {
                _engine.Invoke($"stateEntryAction{state.Id}");
                if (_engineRequestToState != null && state != _engineRequestToState)
                {
                    var s = _engineRequestToState;
                    _engineRequestToState = null;
                    ChangeToState(s);
                }
            }
            catch (Exception e)
            {
                ErrorMessage = $"Error in state entry action ({state.Name}): {e.Message}";
                RunningState = StateMachineRunningState.Error;
            }
        }
    }
}

