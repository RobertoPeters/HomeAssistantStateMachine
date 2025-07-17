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
        return diagram.Nodes.FirstOrDefault(x => (x is StateMachineStateNodeModel) && ((StateMachineStateNodeModel)x).StateId == state.Id);
    }

    public static NodeModel? GetNode(this BlazorDiagram diagram, Information? information)
    {
        if (information == null)
        {
            return null;
        }
        return diagram.Nodes.FirstOrDefault(x => (x is StateMachineInformationNodeModel) && ((StateMachineInformationNodeModel)x).InformationId == information.Id);
    }

    public static State? GetState(this NodeModel node, StateMachine stateMachine)
    {
        if (node is StateMachineStateNodeModel smNode)
        {
            return stateMachine.States.First(x => x.Id == smNode.StateId);
        }
        return null;
    }

    public static Information? GetInformation(this NodeModel node, StateMachine stateMachine)
    {
        if (node is StateMachineInformationNodeModel infNode)
        {
            return stateMachine.Informations.First(x => x.Id == infNode.InformationId);
        }
        return null;
    }

    public static State? GetState(this NodeModel node, ClipboardService.ClipboardContent content)
    {
        if (node is StateMachineStateNodeModel smNode)
        {
            return content.States.First(x => x.Id == smNode.StateId);
        }
        return null;
    }
    
}

