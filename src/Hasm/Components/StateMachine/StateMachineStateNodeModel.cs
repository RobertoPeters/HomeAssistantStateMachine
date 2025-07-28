using Blazor.Diagrams.Core.Geometry;
using Blazor.Diagrams.Core.Models;
using Hasm.Models;
using Hasm.Services.Automations;

namespace Hasm.Components;

public class StateMachineStateNodeModel : NodeModel
{
    public StateMachineStateNodeModel(StateMachineHandler.State state, Automation automation, Point? position = null) : base(position) 
    {
        State = state;
        Automation = automation;
    }

    public StateMachineHandler.State State { get; set; }
    public Automation Automation { get; set; }
}