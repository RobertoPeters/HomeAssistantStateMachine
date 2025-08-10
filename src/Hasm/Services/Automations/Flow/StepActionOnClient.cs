namespace Hasm.Services.Automations.Flow;

public class StepActionOnClient: Step
{
    public const string ClientNameKey = "ClientName";
    public const string VariableNameKey = "VariableName";
    public const string IsGlobalVariableKey = "IsGlobalVariable";
    public const string CommandKey = "Command";
    public const string Parameter1Key = "Parameter1";
    public const string Parameter2Key = "Parameter2";
    public const string Parameter3Key = "Parameter3";

    public override string[] GetStepParameters()
    {
        return [ClientNameKey, VariableNameKey, IsGlobalVariableKey,  CommandKey, Parameter1Key, Parameter2Key, Parameter3Key];
    }

    public override string GetInitializeStatements()
    {
        return $$""""
            step.clientId = getClientId('{{ClientName}}')
            if ({{(string.IsNullOrWhiteSpace(VariableName)).ToString().ToLower()}})
            {
                step.variableId = null
            }
            else
            {
                step.variableId = getVariableIdByName('{{VariableName}}', clientId, {{(!IsGlobalVariable).ToString().ToLower()}})
            }
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
            executeOnClient(step.clientId, step.variableId, '{{Command}}', {{GetParameterString(Parameter1)}}, {{GetParameterString(Parameter2)}}, {{GetParameterString(Parameter3)}})
            return newPayloadStep.currentPayload
            """";
    }

    private string GetParameterString(string? par)
    {
        if (string.IsNullOrWhiteSpace(par))
        {
            return "null";
        }
        return par;
    }

    public string? ClientName
    {
        get => this[ClientNameKey]?.ToString();
        set => this[ClientNameKey] = value;
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

    public bool IsGlobalVariable
    {
        get => (bool?)this[IsGlobalVariableKey] ?? false;
        set => this[IsGlobalVariableKey] = value;
    }

    public string? Command
    {
        get => this[CommandKey]?.ToString();
        set => this[CommandKey] = value;
    }

    public string? Parameter1
    {
        get => this[Parameter1Key]?.ToString();
        set => this[Parameter1Key] = value;
    }

    public string? Parameter2
    {
        get => this[Parameter2Key]?.ToString();
        set => this[Parameter2Key] = value;
    }

    public string? Parameter3
    {
        get => this[Parameter3Key]?.ToString();
        set => this[Parameter3Key] = value;
    }
}
