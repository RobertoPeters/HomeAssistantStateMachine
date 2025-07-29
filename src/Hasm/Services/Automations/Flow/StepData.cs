namespace Hasm.Services.Automations.Flow;

public class StepData
{
    public Guid Id { get; set; }
    public Type Type { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public string? UIData { get; set; }
    public Dictionary<string, object?> StepParameters { get; set; } = [];
    public List<Guid> NextSteps { get; set; } = [];
}