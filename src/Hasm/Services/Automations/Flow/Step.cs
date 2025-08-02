namespace Hasm.Services.Automations.Flow;

public class Step
{
    public StepData StepData { get; set; } = new();

    public static Step FromStepData(StepData stepData)
    {
        var step = (Step)Activator.CreateInstance(stepData.Type)!;
        step.StepData = stepData;
        return step;
    }

    public virtual void Initialize()
    {
        var stepParameters = GetStepParameters();
        foreach (var parameter in stepParameters)
        {
            if (!StepData.StepParameters.ContainsKey(parameter))
            {
                StepData.StepParameters[parameter] = null;
            }
        }
    }

    public object? this[string key]
    {
        get
        {
            if (StepData.StepParameters.TryGetValue(key, out var value))
            {
                return value;
            }
            return null;
        }
        set
        {
            StepData.StepParameters[key] = value;
        }
    }

    public virtual string[] GetStepParameters()
    {
        return [];
    }

    public virtual string GetInitializeStatements()
    {
        return string.Empty;
    }
}