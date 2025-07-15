using Blazor.Diagrams;
using Blazor.Diagrams.Core.Models;
using Hasm.Components;
using Hasm.Models;
using Hasm.Services;

public static class BlazorDiagramExtensions
{
    public static (NodeModel? source, NodeModel? target) GetNodes(this BlazorDiagram diagram, Transition transition, StateMachine stateMachine)
    {
        var source = diagram.GetNode(stateMachine.States.FirstOrDefault(y => y.Id == transition.FromStateId));
        var target = diagram.GetNode(stateMachine.States.FirstOrDefault(y => y.Id == transition.ToStateId));
        return (source, target);
    }

    public static NodeModel? GetNode(this BlazorDiagram diagram, State? state)
    {
        if (state == null)
        {
            return null;
        }
        return diagram.Nodes.FirstOrDefault(x => ((StateMachineStateNodeModel)x).StateId == state.Id);
    }

    public static State? GetState(this NodeModel node, StateMachine stateMachine)
    {
        return stateMachine.States.First(x => x.Id == ((StateMachineStateNodeModel)node).StateId);
    }

    public static State? GetState(this NodeModel node, ClipboardService.ClipboardContent content)
    {
        return content.States.First(x => x.Id == ((StateMachineStateNodeModel)node).StateId);
    }
    
}

