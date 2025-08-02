using Blazor.Diagrams.Core.Geometry;
using Hasm.Models;
using Hasm.Services.Automations.Flow;

namespace Hasm.Components;

public class StepAndNodeModel : StepNodeModel
{
    public StepAndNodeModel(StepAnd step, Automation automation, Point? position = null) : base(step, automation, position) 
    {
        StepAnd = step;
        Automation = automation;
    }

    public StepAnd StepAnd { get; set; }
}