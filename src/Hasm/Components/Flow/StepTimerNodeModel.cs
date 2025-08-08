using Blazor.Diagrams.Core.Geometry;
using Hasm.Models;
using Hasm.Services.Automations.Flow;

namespace Hasm.Components;

public class StepTimerNodeModel : StepNodeModel
{
    public StepTimerNodeModel(StepTimer step, Automation automation, Point? position = null) : base(step, automation, position) 
    {
        StepTimer = step;
        Automation = automation;
    }

    public StepTimer StepTimer { get; set; }
}