using Blazor.Diagrams.Core.Geometry;
using Blazor.Diagrams.Core.Models;
using Hasm.Models;
using Hasm.Services.Automations.StateMachine;

namespace Hasm.Components;

public class StateMachineInformationNodeModel : NodeModel
{
    public StateMachineInformationNodeModel(StateMachineHandler.Information information, Automation automation, Point? position = null) : base(position) 
    {
        Information = information;
        Automation = automation;
    }

    public StateMachineHandler.Information Information { get; set; }
    public Automation Automation { get; set; }
}