namespace HomeAssistantStateMachine.Models;

public class Transition
{
    public int Id { get; set; }
    
    public Guid Handle { get; set; }

    public string? Condition { get; set; }

    public string? UIData { get; set; }

    public State FromState { get; set; } = null!;

    public State ToState { get; set; } = null!;
}
