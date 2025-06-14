using Hasm.Models;

namespace Hasm.Services;

public class StateMachineHandler(StateMachine _stateMachine, IServiceScopeFactory _serviceScopeFactory) : IDisposable
{
    public enum StateMachineRunningState
    {
        NotRunning,
        Running,
        Error
    }

    public StateMachine StateMachine { get; private set; } = _stateMachine;
    public string? ErrorMessage { get; private set; }

    private StateMachineRunningState _runningState = StateMachineRunningState.NotRunning;
    public StateMachineRunningState RunningState 
    { 
        get => _runningState; 
        set
        {
            _runningState = value;
        }
    }

    private State? _currentState = null;
    public State? CurrentState
    {
        get => _currentState;
        private set
        {
            if (value?.Id != _currentState?.Id)
            {
                _currentState = value;
            }
        }
    }


    public const string SystemScript = """"
    -
    """";

    public void Dispose()
    {
    }

    public void Start()
    {

    }

    public Task UpdateAsync(StateMachine stateMachine)
    {
        StateMachine = stateMachine;
        return Task.CompletedTask;
    }
}
