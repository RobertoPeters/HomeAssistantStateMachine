using System.Text;
using Hasm.Models;

namespace Hasm.Services.Automations.Flow;

public static class EngineScriptBuilderFlow
{
    public static string BuildEngineScript(FlowHandler.AutomationProperties properties, Guid instanceId, List<(string variableName, string? variableValue)>? flowParameters)
    {
        var script = new StringBuilder();
        script.AppendLine("var global = this");
        script.AppendLine($"var instanceId = '{instanceId.ToString()}'");
        script.AppendLine();

        script.AppendLine();
        script.AppendLine(SystemMethods.SystemScript(AutomationType.Flow));
        script.AppendLine();

        script.AppendLine();
        script.AppendLine("// Pre-start statemachine action");
        script.AppendLine($"{properties.PreStartAction ?? ""}");

        return script.ToString();
    }

    public static bool ValidateModel(FlowHandler.AutomationProperties properties)
    {
        return true;
    }
}
