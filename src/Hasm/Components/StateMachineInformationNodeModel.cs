using Blazor.Diagrams.Core.Geometry;
using Blazor.Diagrams.Core.Models;
using Hasm.Models;

namespace Hasm.Components;

public class StateMachineInformationNodeModel : NodeModel
{
    public StateMachineInformationNodeModel(Guid informationId, StateMachine stateMachine, Point? position = null) : base(position) 
    {
        InformationId = informationId;
        StateMachine = stateMachine;
    }

    public Guid InformationId { get; set; }
    public StateMachine StateMachine { get; set; }
}