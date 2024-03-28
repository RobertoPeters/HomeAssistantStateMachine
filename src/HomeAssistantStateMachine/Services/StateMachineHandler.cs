using HomeAssistantStateMachine.Models;

namespace HomeAssistantStateMachine.Services;

public class StateMachineHandler: IDisposable
{
    private StateMachineService _stateMachineService;
    private StateMachine _stateMachine;

    public StateMachineHandler(StateMachineService stateMachineService, StateMachine stateMachine)
    {
        _stateMachineService = stateMachineService;
        _stateMachine = stateMachine;
    }

    public void Dispose()
    {
        //todo
    }
}
