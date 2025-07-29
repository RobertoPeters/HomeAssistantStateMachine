using Hasm.Models;

namespace Hasm.Services.Automations;

public static class EngineScriptBuilder
{
    public static string BuildEngineScriptForEditor(Automation automation)
    {
        if (automation.AutomationType == AutomationType.StateMachine)
        {
            return EngineScriptBuilderStateMachine.BuildEngineScript(StateMachineHandler.GetAutomationProperties(automation.Data), true, Guid.Empty, null);
        }
        return "";
    }
}
