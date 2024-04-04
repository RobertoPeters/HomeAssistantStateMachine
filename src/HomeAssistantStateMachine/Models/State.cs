using System.ComponentModel.DataAnnotations;

namespace HomeAssistantStateMachine.Models;

public class State
{
    public int Id { get; set; }

    public int? StateMachineId { get; set; }
    public StateMachine? StateMachine { get; set; }

    [StringLength(255)]
    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public string? EntryAction { get; set; }

    public string? UIData { get; set; }

}
