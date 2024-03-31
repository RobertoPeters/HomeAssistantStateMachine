﻿namespace HomeAssistantStateMachine.Models;

public class Transition
{
    public int Id { get; set; }
    
     public string? Condition { get; set; }

    public string? UIData { get; set; }

    public State? FromState { get; set; }

    public State? ToState { get; set; }
}
