namespace Hasm.Services.Automations.Flow;

public class StepSetVariableValue: Step
{
    public const string VariableNameKey = "VariableName";
    public const string ClientNameKey = "ClientName";
    public const string IsGlobalVariableKey = "IsGlobalVariable";
    public const string PayloadAsValueKey = "PayloadAsValue";
    public const string VariableValueKey = "VariableValue";

    public override string[] GetStepParameters()
    {
        return [VariableNameKey, ClientNameKey, IsGlobalVariableKey, PayloadAsValueKey];
    }

    public override string GetInitializeStatements()
    {
        return $$""""
            var clientId = getClientId('{{ClientName}}')
            step.variableId = getVariableIdByName('{{VariableName}}', clientId, {{(!IsGlobalVariable).ToString().ToLower()}})
            return null
            """";
    }

    public override string GetPayloadStatements()
    {
        return $$""""
            if (newPayloadStep == null) {
                return step.currentPayload
            }
            if (newPayloadStep.currentPayload == null) {
                return null
            }
            if ({{PayloadAsValue.ToString().ToLower()}}) {
                setVariableValue(step.variableId, newPayloadStep.currentPayload)
                return newPayloadStep.currentPayload
            }
            setVariableValue(step.variableId, {{VariableValue}})
            return '{{VariableValue}}'
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

    public bool PayloadAsValue
    {
        get => (bool?)this[PayloadAsValueKey] ?? false;
        set => this[PayloadAsValueKey] = value;
    }
    public string? VariableValue
    {
        get => this[VariableValueKey]?.ToString();
        set => this[VariableValueKey] = value;
    }
}
