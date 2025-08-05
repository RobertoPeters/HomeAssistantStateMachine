using Blazor.Diagrams.Core.Geometry;
using Hasm.Models;
using Hasm.Services.Automations.Flow;

namespace Hasm.Components;

public class StepScriptNodeModel : StepNodeModel
{
    public StepScriptNodeModel(StepScript step, Automation automation, Point? position = null) : base(step, automation, position) 
    {
        StepScript = step;
        Automation = automation;
    }

    public StepScript StepScript { get; set; }
}