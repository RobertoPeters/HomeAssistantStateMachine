using System.ComponentModel.DataAnnotations;

namespace HomeAssistantStateMachine.Models;

public class VariableValue
{
    public int Id { get; set; }
    
    public Guid Handle { get; set; }

    public Variable? Variable { get; set; }

    public string? Value { get; set; }

    public DateTime Update { get; set; }
}
