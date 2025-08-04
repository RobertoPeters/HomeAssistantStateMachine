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
                if (value is System.Text.Json.JsonElement jsonElement)
                {
                    if (jsonElement.ValueKind == System.Text.Json.JsonValueKind.Null)
                    {
                        return null;
                    }
                    if (jsonElement.ValueKind == System.Text.Json.JsonValueKind.False)
                    {
                        return false;
                    }
                    if (jsonElement.ValueKind == System.Text.Json.JsonValueKind.True)
                    {
                        return true;
                    }
                }
                return value;
            }
            return null;
        }
        set
        {
            StepData.StepParameters[key] = value;
        }
    }

    public string? Name
    {
        get => StepData.Name;
        set => StepData.Name = value;
    }

    public string? Description
    {
        get => StepData.Description;
        set => StepData.Description = value;
    }

    public string Title => !string.IsNullOrWhiteSpace(Name) ? Name : Description ?? "";

    public virtual string[] GetStepParameters()
    {
        return [];
    }

    public virtual string GetInitializeStatements()
    {
        return "return null";
    }
    public virtual string GetPayloadStatements()
    {
        return "return null";
    }

    public virtual string GetPayloadEqualStatements()
    {
        return "return payload1 == payload2";
    }
}