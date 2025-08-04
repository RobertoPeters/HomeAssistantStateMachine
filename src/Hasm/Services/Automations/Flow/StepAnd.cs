namespace Hasm.Services.Automations.Flow;

public class StepAnd: Step
{
    public StepAnd()
    {
        Description = "And";
    }

    public override string GetPayloadStatements()
    {
        return """"
                var inputSteps = getSteps(step.inputSteps)
                var nullPayloads = inputSteps.filter((inputStep) => inputStep.currentPayload == null || inputStep.currentPayload == '')
                if (nullPayloads.length > 0) 
                {
                    return null
                }
                var allTruePayloads = inputSteps.filter((inputStep) => isTrue(inputStep.currentPayload))
                if (inputSteps.length > 0 && allTruePayloads.length == inputSteps.length) 
                {
                    return true
                }
                return false
            """";
    }
}
