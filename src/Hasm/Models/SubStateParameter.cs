namespace Hasm.Models;

public class SubStateParameter
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string ScriptVariableName { get; set; }
}
