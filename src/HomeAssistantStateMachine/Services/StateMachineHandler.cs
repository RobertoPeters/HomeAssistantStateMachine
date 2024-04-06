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

    private Jint.Engine? _engine = null;
    private volatile bool _readyForTriggers = false;
    private readonly SynchronizationContext _syncContext;
    private readonly VariableService _variableService;
    private readonly HAClientService _haClientService;

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

    public StateMachineHandler(StateMachine stateMachine, VariableService variableService, HAClientService haClientService)
    {
        StateMachine = stateMachine;
        _variableService = variableService;
        _haClientService = haClientService;
        _syncContext = SynchronizationContext.Current ?? new SynchronizationContext();

    }

    public void Dispose()
    {
        _readyForTriggers = false;
        _syncContext.Send((object? state) =>
        {
            RunningState = StateMachineRunningState.NotRunning;
            _engine?.Dispose();
            _engine = null;
        }, null);
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
                var allLines = state.EntryAction.Split(['\n', '\r'], StringSplitOptions.RemoveEmptyEntries);
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
            var allLines = stateMachine.PreScheduleAction.Split(['\n', '\r'], StringSplitOptions.RemoveEmptyEntries);
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
        _syncContext.Send((_) =>
        {
            _currentState = null;
            if (ValidateModel())
            {
                _engine = new Jint.Engine();
                _engine.SetValue("system", NewSystemMethods);

                try
                {
                    _engine.Execute(BuildEngineScript(StateMachine));
                    RunningState = StateMachineRunningState.Running;
                    _engine.Invoke("preScheduleAction");
                    ChangeToState(GetStartState());
                }
                catch (Exception e)
                {
                    _engine.Dispose();
                    _engine = null;
                    ErrorMessage = $"Error initializing statemachine: {e.Message}";
                    RunningState = StateMachineRunningState.Error;
                }
            }
            else
            {
                ErrorMessage = $"Statemachine is not valid";
                RunningState = StateMachineRunningState.Error;
            }
        }, null);
        _readyForTriggers = true;
    }

    public void Stop()
    {
        _readyForTriggers = false;
        _syncContext.Send((_) =>
        {
            RunningState = StateMachineRunningState.NotRunning;
            _engine?.Dispose();
            _engine = null;
            ChangeToState(null);
        }, null);
    }

    public void TriggerProcess()
    {
        if (!_readyForTriggers) return;

        _syncContext.Send((_) =>
        {

            if (RunningState == StateMachineRunningState.Running && CurrentState != null && _engine != null)
            {
                Transition? activeTransition = null;
                try
                {
                    _engine.Invoke("preScheduleAction");
                    var transitions = StateMachine.Transitions
                        .Where(t => t.FromStateId == CurrentState.Id)
                        .ToList();
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
                catch (Exception e)
                {
                    ErrorMessage = $"Error in transition ({activeTransition?.Description ?? activeTransition?.Condition}): {e.Message}";
                    RunningState = StateMachineRunningState.Error;
                }
            }
        }, null);
    }

    public void UpdateStateMachine(StateMachine stateMachine)
    {
        _syncContext.Send((_) =>
        {
            Stop();
            StateMachine = stateMachine;
            Start();
        }, null);
    }

    private void ChangeToState(State? state)
    {
        CurrentState = state;
        if (RunningState == StateMachineRunningState.Running && _engine != null && state != null)
        {
            try
            {
                _engine.Invoke($"stateEntryAction{state.Id}");
            }
            catch (Exception e)
            {
                ErrorMessage = $"Error in state entry action ({state.Name}): {e.Message}";
                RunningState = StateMachineRunningState.Error;
            }
        }
    }
}

