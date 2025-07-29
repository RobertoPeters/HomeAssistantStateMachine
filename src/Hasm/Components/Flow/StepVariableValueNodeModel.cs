using Blazor.Diagrams.Core.Geometry;
using Hasm.Models;
using Hasm.Services.Automations.Flow;

namespace Hasm.Components;

public class StepVariableValueNodeModel : StepNodeModel
{
    public StepVariableValueNodeModel(StepVariableValue step, Automation automation, Point? position = null) : base(step, automation, position) 
    {
        StepVariableValue = step;
        Automation = automation;
    }

    public StepVariableValue StepVariableValue { get; set; }
}