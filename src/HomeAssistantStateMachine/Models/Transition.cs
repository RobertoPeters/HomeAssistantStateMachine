namespace HomeAssistantStateMachine.Models;

public class Transition
{
    public int Id { get; set; }

    public string? Description { get; set; }

    public string? Condition { get; set; }

    public string? UIData { get; set; }

    public int? StateMachineId { get; set; }
    public StateMachine? StateMachine { get; set; }

    public int? FromStateId { get; set; }
    public State? FromState { get; set; }

    public int? ToStateId { get; set; }
    public State? ToState { get; set; }
}
