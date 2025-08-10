using Blazor.Diagrams.Core.Geometry;
using Hasm.Models;
using Hasm.Services.Automations.Flow;

namespace Hasm.Components;

public class StepActionOnClientNodeModel : StepNodeModel
{
    public StepActionOnClientNodeModel(StepActionOnClient step, Automation automation, Point? position = null) : base(step, automation, position) 
    {
        StepActionOnClient = step;
        Automation = automation;
    }

    public StepActionOnClient StepActionOnClient { get; set; }
}