namespace Hasm.Models;

public class VariableValue: ModelBase
{
    public int VariableId { get; set; }

    public string? Value { get; set; }

    public DateTime Update { get; set; }

}
