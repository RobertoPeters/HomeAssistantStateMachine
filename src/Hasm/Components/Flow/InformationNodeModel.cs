using Blazor.Diagrams.Core.Geometry;
using Blazor.Diagrams.Core.Models;
using Hasm.Models;
using Hasm.Services.Automations.Flow;

namespace Hasm.Components;

public class InformationNodeModel : NodeModel
{
    public InformationNodeModel(Information information, Automation automation, Point? position = null) : base(position) 
    {
        Information = information;
        Automation = automation;
    }

    public Information Information { get; set; }
    public Automation Automation { get; set; }
}