using System.Text.Json.Serialization;

namespace Hasm.Services.Automations.Flow;

public class StepData
{
    public Guid Id { get; set; }
    public string StepTypeName { get; set; } = null!;
    public string? Name { get; set; } = null!;
    public bool HasInput { get; set; } = true;
    public string? Description { get; set; }
    public string? UIData { get; set; }
    public Dictionary<string, object?> StepParameters { get; set; } = [];
    public List<Guid> NextSteps { get; set; } = [];

    [JsonIgnore]
    private Type? _type;
    
    [JsonIgnore]
    public Type Type 
    { 
        get
        {
            if (_type == null)
            {
                _type = Type.GetType(StepTypeName)!;
            }
            return _type!;
        }
        set
        {
            _type = value;
            StepTypeName = value.FullName!;
        }
    }
}