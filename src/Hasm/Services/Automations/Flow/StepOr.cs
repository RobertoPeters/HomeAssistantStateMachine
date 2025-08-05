namespace Hasm.Services.Automations.Flow;

public class StepOr: Step
{
    public StepOr()
    {
        Description = "Or";
    }

    public override string GetPayloadStatements()
    {
        return """"
                var inputSteps = getSteps(step.inputSteps)
                if (inputSteps.filter((inputStep) => isTrue(inputStep.currentPayload)).length > 0) 
                {
                    return true
                }
                return false
            """";
    }
}
