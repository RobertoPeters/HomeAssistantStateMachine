namespace Hasm.Services.Automations.Flow;

public class StepVariableValue: Step
{
    public const string VariableNameKey = "VariableName";
    public const string ClientNameKey = "ClientName";
    public const string IsGlobalVariableKey = "IsGlobalVariable";
    public const string IsPersistantKey = "IsPersistant";
    public const string DataKey = "Data";
    public const string MockingValuesKey = "MockingValues";
    public const string PayloadOnStartKey = "PayloadOnStart";

    public override string[] GetStepParameters()
    {
        return [VariableNameKey, ClientNameKey, IsGlobalVariableKey, IsPersistantKey, DataKey, MockingValuesKey];
    }

    public override string GetInitializeStatements()
    {
        return "return null";
    }

    public override string GetPayloadStatements()
    {
        return "return null";
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

    public bool IsGlobalVariable
    {
        get => (bool?)this[IsGlobalVariableKey] ?? false;
        set => this[IsGlobalVariableKey] = value;
    }

    public bool PayloadOnStart
    {
        get => (bool?)this[PayloadOnStartKey] ?? false;
        set => this[PayloadOnStartKey] = value;
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
