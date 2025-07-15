using Blazor.Diagrams.Core.Geometry;
using Blazor.Diagrams.Core.Models;

namespace Hasm.Components;

public class StateMachineStateNodeModel : NodeModel
{
    public StateMachineStateNodeModel(Guid stateId, Point? position = null) : base(position) 
    {
        StateId = stateId;
    }

    public Guid StateId { get; set; }
}