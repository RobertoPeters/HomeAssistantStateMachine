using Hasm.Models;
using Hasm.Services.Automations.Flow;
using Hasm.Services.Automations.StateMachine;

namespace Hasm.Services.Automations;

public static class EngineScriptBuilder
{
    public static string BuildEngineScriptForEditor(Automation automation)
    {
        if (automation.AutomationType == AutomationType.StateMachine)
        {
            return EngineScriptBuilderStateMachine.BuildEngineScript(StateMachineHandler.GetAutomationProperties(automation.Data), true, Guid.Empty, null);
        }
        else if (automation.AutomationType == AutomationType.Flow)
        {
            return EngineScriptBuilderFlow.BuildEngineScript(FlowHandler.GetAutomationProperties(automation.Data), Guid.Empty, null);
        }
        return "";
    }
}
