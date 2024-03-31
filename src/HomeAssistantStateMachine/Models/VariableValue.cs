using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HomeAssistantStateMachine.Models;

public class VariableValue
{
    public int Id { get; set; }

    public int VariableId { get; set; }
    public Variable? Variable { get; set; }

    public string? Value { get; set; }

    public DateTime Update { get; set; }
}
