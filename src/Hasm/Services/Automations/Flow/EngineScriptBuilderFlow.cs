using System.Text;
using Hasm.Models;

namespace Hasm.Services.Automations.Flow;

public static class EngineScriptBuilderFlow
{
    public static string BuildEngineScript(FlowHandler.AutomationProperties properties, Guid instanceId, List<(string variableName, string? variableValue)>? flowParameters)
    {
        properties.CreateStepsFromStepDatas();

        var script = new StringBuilder();
        script.AppendLine("var global = this");
        script.AppendLine($"var instanceId = '{instanceId.ToString()}'");
        script.AppendLine();

        script.AppendLine();
        script.AppendLine(SystemMethods.SystemScript(AutomationType.Flow));
        script.AppendLine();

        script.AppendLine("var steps = []");
        foreach (var step in properties.Steps.ToList())
        {
            script.AppendLine($$""""
            steps.push({
                'id': '{{StepId(step)}}',
                'initialPayloadFunction': function(){
                {{step.GetInitializeStatements()}}
                },
                'getPayloadFunction': function(){
                {{step.GetPayloadStatements()}}
                },
                'currentPayload': null,
                'inputSteps': [{{string.Join(", ", properties.Steps.Where(x => x.StepData.NextSteps.Contains(step.StepData.Id)).Select(x => $"'{StepId(x)}'").ToList())}}],
                'inputPayloads': [{{string.Join(", ", properties.Steps.Where(x => x.StepData.NextSteps.Contains(step.StepData.Id)).Select(x => "null").ToList())}}]
            })           
            """");
        }

        script.AppendLine(""""
            function schedule() {
            }
            
            """");

        script.AppendLine();
        script.AppendLine("// Pre-start statemachine action");
        script.AppendLine($"{properties.PreStartAction ?? ""}");

        script.AppendLine(""""
            steps.forEach(function(step) {
                step.currentPayload = step.initialPayloadFunction();
            });
            
            """");

        return script.ToString();
    }

    public static bool ValidateModel(FlowHandler.AutomationProperties properties)
    {
        return true;
    }

    private static string StepId(Step step)
    {
        return step.StepData.Id.ToString("N");
    }
}
