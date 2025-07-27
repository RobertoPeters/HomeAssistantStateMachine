using Blazor.Diagrams;
using Blazor.Diagrams.Core.Models;
using Hasm.Components;
using Hasm.Services;

public static class BlazorDiagramExtensions
{
    public static (NodeModel? source, NodeModel? target) GetNodes(this BlazorDiagram diagram, StateMachineHandler.Transition transition, StateMachineHandler.AutomationProperties automationProperties)
    {
        var source = diagram.GetNode(automationProperties.States.FirstOrDefault(y => y.Id == transition.FromStateId));
        var target = diagram.GetNode(automationProperties.States.FirstOrDefault(y => y.Id == transition.ToStateId));
        return (source, target);
    }

    public static NodeModel? GetNode(this BlazorDiagram diagram, StateMachineHandler.State? state)
    {
        if (state == null)
        {
            return null;
        }
        return diagram.Nodes.FirstOrDefault(x => (x is StateMachineStateNodeModel) && ((StateMachineStateNodeModel)x).State.Id == state.Id);
    }

    public static NodeModel? GetNode(this BlazorDiagram diagram, StateMachineHandler.Information? information)
    {
        if (information == null)
        {
            return null;
        }
        return diagram.Nodes.FirstOrDefault(x => (x is StateMachineInformationNodeModel) && ((StateMachineInformationNodeModel)x).Information.Id == information.Id);
    }

    public static StateMachineHandler.State? GetState(this NodeModel node, StateMachineHandler.AutomationProperties automationProperties)
    {
        if (node is StateMachineStateNodeModel smNode)
        {
            return automationProperties.States.First(x => x.Id == smNode.State.Id);
        }
        return null;
    }

    public static StateMachineHandler.Information? GetInformation(this NodeModel node, StateMachineHandler.AutomationProperties automationProperties)
    {
        if (node is StateMachineInformationNodeModel infNode)
        {
            return automationProperties.Informations.First(x => x.Id == infNode.Information.Id);
        }
        return null;
    }

    public static StateMachineHandler.State? GetState(this NodeModel node, ClipboardService.ClipboardContent content)
    {
        if (node is StateMachineStateNodeModel smNode)
        {
            return content.States.First(x => x.Id == smNode.State.Id);
        }
        return null;
    }
    
}

