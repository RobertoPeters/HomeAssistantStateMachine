namespace Hasm.Models;

public class StateMachine: ModelBase
{
    public string Name { get; set; } = null!;
    public bool Enabled { get; set; }
    public bool IsSubStateMachine { get; set; }
    public string? PreStartAction { get; set; }
    public string? PreScheduleAction { get; set; }
    public List<State> States { get; set; } = [];
    public List<Transition> Transitions { get; set; } = [];
    public List<SubStateMachineParameter> SubStateMachineParameters { get; set; } = [];
}
