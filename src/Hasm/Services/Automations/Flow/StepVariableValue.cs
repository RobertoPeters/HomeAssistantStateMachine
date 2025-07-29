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
        set => this[VariableNameKey] = value;
    }
}
