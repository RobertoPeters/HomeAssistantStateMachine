using Blazor.Diagrams.Core.Geometry;
using Blazor.Diagrams.Core.Models;
using Hasm.Models;
using Hasm.Services.Automations.Flow;

namespace Hasm.Components;

public class StepNodeModel : NodeModel
{
    public StepNodeModel(Step step, Automation automation, Point? position = null) : base(position) 
    {
        Step = step;
        Automation = automation;
    }

    public Step Step { get; set; }
    public Automation Automation { get; set; }
}