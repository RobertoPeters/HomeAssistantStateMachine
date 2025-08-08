namespace Hasm.Services.Automations.Flow;

public class StepTimer: Step
{
    public const string VariableNameKey = "VariableName";
    public const string TimeoutSecondsKey = "TimeoutSeconds";

    public StepTimer()
    {
        Description = "Timer";
    }

    public override string[] GetStepParameters()
    {
        return [VariableNameKey, TimeoutSecondsKey];
    }

    public override string GetInitializeStatements()
    {
        return $$""""
            step.timerVariableId = createVariableOnClient('{{VariableName}}', timerClientId, true, false, {{TimeoutSeconds ?? 10}}, [0, 10])
            step.timerIsRunning = false
            """";
    }

    public override string GetPayloadStatements()
    {
        return $$""""
            var variableId = step.timerVariableId
            if (newPayloadStep == null) {
                if (!step.timerIsRunning) {
                    return null
                }

                //check timer variable
                var variableValue = getVariableValue(variableId)
                if (variableValue == "0" || variableValue == 0) {
                    return true
                }
                return null
            } else if (newPayloadStep.currentPayload == null || newPayloadStep.currentPayload == '' ) {
                //cancel timer
                step.timerIsRunning = false
                cancelTimerVariable(variableId)
                return null
            } else {
                //restart timer
                step.timerIsRunning = true
                startTimerVariable(variableId)
                return null
            }
            """";
     }

    public string? VariableName
    {
        get => this[VariableNameKey]?.ToString();
        set
        {
            this[VariableNameKey] = value;
            Description = value;
        }
    }

    public int? TimeoutSeconds
    {
        get => (int?)this[TimeoutSecondsKey] ?? 10;
        set => this[TimeoutSecondsKey] = value;
    }
}
