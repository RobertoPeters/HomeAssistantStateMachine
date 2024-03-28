using System.ComponentModel.DataAnnotations;

namespace HomeAssistantStateMachine.Models;

public class Variable
{
    public int Id { get; set; }
    
    public Guid Handle { get; set; }

    [MaxLength(255)]
    public string Name { get; set; } = null!;

    public HAClient? HAClient { get; set; }

    public StateMachine? StateMachine { get; set; }

    public State? State { get; set; }

    public string? Data { get; set; }
}
