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
            script.AppendLine(
            $$""""
            steps.push({
                'id': '{{StepId(step)}}',
                'name': '{{step.Name ?? step.Description}}',
                'initialPayloadFunction': function(step){
                {{step.GetInitializeStatements()}}
                },
                'getPayloadFunction': function(step, newPayloadStep){
                {{step.GetPayloadStatements()}}
                },
                'currentPayload': null,
                'payloadUpdatedAt': new Date(),
                'inputSteps': [{{string.Join(", ", properties.Steps.Where(x => x.StepData.NextSteps.Contains(step.StepData.Id)).Select(x => $"'{StepId(x)}'").ToList())}}],
                'getPayloadEqualfunction': function(payload1, payload2){
                {{step.GetPayloadEqualStatements()}}
                }
            })           
            """");
        }

        script.AppendLine(""""

            function getStepsWithInput(inputStepId) {
                return steps.filter(function(step) {
                    return step.inputSteps.includes(inputStepId);
                })
            }

            function checkStep(step, newPayloadStep) {
                var newPayload = step.getPayloadFunction(step, newPayloadStep);
                if (!step.getPayloadEqualfunction(step.currentPayload, newPayload)) {
                    step.currentPayload = newPayload
                    step.payloadUpdatedAt = new Date();
                    log('payload changed: step="' + step.name + '", payload="' + newPayload + '"')
                    updatePayload(step.id, newPayload);

                    var affectedSteps = getStepsWithInput(step.id);
                    affectedSteps.forEach(function(affectedStep) {
                        checkStep(affectedStep, step)
                    })
                }
            }

            function schedule() {
                steps.forEach(function(step) {
                    checkStep(step, null)
                })
            }
                        
            """");

        script.AppendLine();
        script.AppendLine("// Pre-start statemachine action");
        script.AppendLine($"{properties.PreStartAction ?? ""}");

        script.AppendLine(""""
            steps.forEach(function(step) {
                step.currentPayload = step.initialPayloadFunction(step)
            })
            
            """");

        return script.ToString();
    }

    public static bool ValidateModel(FlowHandler.AutomationProperties properties)
    {
        return true;
    }

    private static string StepId(Step step)
    {
        return step.StepData.Id.ToString();
    }
}
