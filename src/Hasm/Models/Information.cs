using System.Text.Json.Serialization;

namespace Hasm.Models;

public class Information
{
    public Guid Id { get; set; }
    public string? Description { get; set; }
    public string? Evaluation { get; set; }
    public string? UIData { get; set; }

    [JsonIgnore]
    public string? EvaluationResult { get; set; }
}
