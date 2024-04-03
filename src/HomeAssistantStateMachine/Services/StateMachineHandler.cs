using HomeAssistantStateMachine.Models;
using Jint.Native;
using System.Text;

namespace HomeAssistantStateMachine.Services;

public class StateMachineHandler : IDisposable
{
    public enum StateMachineRunningState
    {
        NotRunning,
        Running,
        Error
    }

    public StateMachine StateMachine { get; private set; }
    public StateMachineRunningState RunningState { get; private set; } = StateMachineRunningState.NotRunning;

    private Jint.Engine? _engine = null;
    private readonly SynchronizationContext _syncContext;

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

    public StateMachineHandler(StateMachine stateMachine)
    {
        StateMachine = stateMachine;
        _syncContext = SynchronizationContext.Current ?? new SynchronizationContext();
    }

    public void Dispose()
    {
        _syncContext.Send((object? state) =>
        {
            RunningState = StateMachineRunningState.NotRunning;
            _engine?.Dispose();
            _engine = null;
        }, null);
    }

    private bool ValidateModel()
    {
        //do we have one start state?
        if (!StateMachine.States.Any())
        {
            return false;
        }

        var statesWithoutTransitionEntry = StateMachine.States
            .Join(StateMachine.Transitions, s => s.Id, t => t.ToStateId, (s, t) => new { State = s, Transition = t })
            .ToList();

        if (statesWithoutTransitionEntry.Count != 1)
        {
            return false;
        }


        return true;
    }

    public void Start()
    {
        _syncContext.Post((_) =>
        {
            if (ValidateModel())
            {
                var startState = StateMachine.States
                     .Join(StateMachine.Transitions, s => s.Id, t => t.ToStateId, (s, t) => s)
                     .First();

                _engine = new Jint.Engine();

                StringBuilder script = new StringBuilder();
                foreach (var state in StateMachine.States)
                {
                    script.AppendLine($"function stateEntryAction{state.Id}() {{ {state.EntryAction ?? ""} }}");
                }

                foreach (var transition in StateMachine.Transitions)
                {
                    script.AppendLine($"function transitionResult{transition.Id}() {{ return {transition.Condition ?? "true"} ; }}");
                }

                try
                {
                    _engine.Execute(script.ToString());
                    ChangeToState(startState);
                }
                catch
                {
                    _engine.Dispose();
                    _engine = null;
                    RunningState = StateMachineRunningState.Error;
                }
            }
            else
            {
                RunningState = StateMachineRunningState.Error;
            }
        }, null);
    }

    public void Stop()
    {
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
        _syncContext.Post((_) =>
        {

            if (RunningState == StateMachineRunningState.Running && CurrentState != null && _engine != null)
            {
                try
                {
                    var transitions = StateMachine.Transitions
                        .Where(t => t.FromStateId == CurrentState.Id)
                        .ToList();
                    foreach (var transition in transitions)
                    {
                        if (_engine.Invoke($"transitionResult{transition.Id}") == JsBoolean.True)
                        {
                            ChangeToState(StateMachine.States.First(s => s.Id == transition.ToStateId));
                            break;
                        }
                    }
                }
                catch
                {
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
        if (RunningState == StateMachineRunningState.Running && _engine != null && state != null)
        {
            try
            {
                _engine.Invoke($"stateEntryAction{state.Id}");
            }
            catch
            {
                RunningState = StateMachineRunningState.Error;
            }
        }
        CurrentState = state;
    }
}

