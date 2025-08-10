using Blazor.Diagrams.Core.Geometry;
using Hasm.Models;
using Hasm.Services.Automations.Flow;

namespace Hasm.Components;

public class StepSetVariableValueNodeModel : StepNodeModel
{
    public StepSetVariableValueNodeModel(StepSetVariableValue step, Automation automation, Point? position = null) : base(step, automation, position) 
    {
        StepSetVariableValue = step;
        Automation = automation;
    }

    public StepSetVariableValue StepSetVariableValue { get; set; }
}