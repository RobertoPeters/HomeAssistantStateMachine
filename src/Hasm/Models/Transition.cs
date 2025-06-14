namespace Hasm.Models;

public class Transition
{
    public Guid Id { get; set; }
    public string? Description { get; set; }
    public string? Condition { get; set; }
    public string? UIData { get; set; }
    public Guid? FromStateId { get; set; }
    public Guid? ToStateId { get; set; }
}
