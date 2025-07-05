namespace Hasm.Models;

public class SubStateMachineParameter
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = null!;
    public string ScriptVariableName { get; set; } = null!;
    public string? DefaultValue { get; set; }
    public bool IsOutput { get; set; }
    public bool IsInput { get; set; }
}
