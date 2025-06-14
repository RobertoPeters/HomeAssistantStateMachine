namespace Hasm.Models;

public class Variable: ModelBase
{
    public string Name { get; set; } = null!;
    public int ClientId { get; set; }
    public List<string>? MockingValues { get; set; }
    public int? StateMachineId { get; set; }
    public string? Data { get; set; }
}
