using System.Text;
using Hasm.Models;

namespace Hasm.Services.Automations.Script;

public static class EngineScriptBuilderScript
{
    public static string BuildEngineScript(ScriptHandler.AutomationProperties properties, Guid instanceId, List<(string variableName, string? variableValue)>? scriptParameters)
    {
        var script = new StringBuilder();
        script.AppendLine("var global = this");
        script.AppendLine($"var instanceId = '{instanceId.ToString()}'");
        script.AppendLine();

        script.AppendLine();
        script.AppendLine(SystemMethods.SystemScript(AutomationType.Script));
        script.AppendLine();

        if (string.IsNullOrWhiteSpace(properties.Script))
        {
            script.AppendLine(
            $$""""
            function schedule() {
            }
            """");
        }
        else
        {
            script.AppendLine();
            script.AppendLine(properties.Script);
            script.AppendLine();
        }

        return script.ToString();
    }
}
