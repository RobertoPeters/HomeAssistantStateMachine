using Blazor.Diagrams.Core.Geometry;
using Hasm.Models;
using Hasm.Services.Automations.Flow;

namespace Hasm.Components;

public class StepOrNodeModel : StepNodeModel
{
    public StepOrNodeModel(StepOr step, Automation automation, Point? position = null) : base(step, automation, position) 
    {
        StepOr = step;
        Automation = automation;
    }

    public StepOr StepOr { get; set; }
}