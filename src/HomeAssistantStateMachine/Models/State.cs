using System.ComponentModel.DataAnnotations;

namespace HomeAssistantStateMachine.Models;

public class State
{
    public int Id { get; set; }
    
    [StringLength(255)]
    public string Name { get; set; } = null!;
    
    public string? EntryAction { get; set; }

    public string? UIData { get; set; }

}
