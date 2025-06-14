namespace Hasm.Models;

public class State
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public bool IsErrorState { get; set; }
    public bool IsStartState { get; set; }
    public string? Description { get; set; }
    public string? EntryAction { get; set; }
    public string? UIData { get; set; }
}
