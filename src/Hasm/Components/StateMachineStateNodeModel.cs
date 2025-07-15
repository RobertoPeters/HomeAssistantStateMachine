using Blazor.Diagrams.Core.Geometry;
using Blazor.Diagrams.Core.Models;
using Hasm.Models;

namespace Hasm.Components;

public class StateMachineStateNodeModel : NodeModel
{
    public StateMachineStateNodeModel(Guid stateId, StateMachine stateMachine, Point? position = null) : base(position) 
    {
        StateId = stateId;
        StateMachine = stateMachine;
    }

    public Guid StateId { get; set; }
    public StateMachine StateMachine { get; set; }
}