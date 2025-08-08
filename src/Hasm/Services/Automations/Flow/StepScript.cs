namespace Hasm.Services.Automations.Flow;

public class StepScript: Step
{
    public const string InitializeStatementsKey = "InitializeStatements";
    public const string PayloadStatementsKey = "PayloadStatements";
    public const string PayloadEqualStatementsKey = "PayloadEqualStatements";

    public StepScript()
    {
        Description = "Script";
    }

    public override string[] GetStepParameters()
    {
        return [InitializeStatementsKey, PayloadStatementsKey, PayloadEqualStatementsKey];
    }

    public override string GetInitializeStatements()
    {
        return string.IsNullOrWhiteSpace(InitializeStatements) ? "return null" : InitializeStatements;
    }

    public override string GetPayloadStatements()
    {
        return string.IsNullOrWhiteSpace(PayloadStatements) ? "return null" : PayloadStatements;
    }

    public override string GetPayloadEqualStatements()
    {
        return string.IsNullOrWhiteSpace(PayloadEqualStatements) ? "return true" : PayloadEqualStatements;
    }

    public string? InitializeStatements
    {
        get => this[InitializeStatementsKey]?.ToString();
        set => this[InitializeStatementsKey] = value;
    }

    public string? PayloadStatements
    {
        get => this[PayloadStatementsKey]?.ToString();
        set => this[PayloadStatementsKey] = value;
    }

    public string? PayloadEqualStatements
    {
        get => this[PayloadEqualStatementsKey]?.ToString();
        set => this[PayloadEqualStatementsKey] = value;
    }
}
