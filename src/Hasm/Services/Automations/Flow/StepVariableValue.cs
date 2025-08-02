namespace Hasm.Services.Automations.Flow;

public class StepVariableValue: Step
{
    public const string VariableNameKey = "VariableName";
    public const string ClientNameKey = "ClientName";
    public const string IsFlowVariableKey = "IsFlowVariable";
    public const string IsPersistantKey = "IsPersistant";
    public const string DataKey = "Data";
    public const string MockingValuesKey = "MockingValues";

    public override string[] GetStepParameters()
    {
        return [VariableNameKey, ClientNameKey, IsFlowVariableKey, IsPersistantKey, DataKey, MockingValuesKey];
    }

    public override string GetInitializeStatements()
    {
        return string.Empty;
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

    public string? ClientName
    {
        get => this[ClientNameKey]?.ToString();
        set => this[ClientNameKey] = value;
    }

    public bool IsFlowVariable
    {
        get => (bool?)this[IsFlowVariableKey] ?? false;
        set => this[IsFlowVariableKey] = value;
    }

    public bool IsPersistant
    {
        get => (bool?)this[IsPersistantKey] ?? false;
        set => this[IsPersistantKey] = value;
    }

    public string? Data
    {
        get => this[DataKey]?.ToString();
        set => this[DataKey] = value;
    }

    public string? MockingValues
    {
        get => this[MockingValuesKey]?.ToString();
        set => this[MockingValuesKey] = value;
    }
}
