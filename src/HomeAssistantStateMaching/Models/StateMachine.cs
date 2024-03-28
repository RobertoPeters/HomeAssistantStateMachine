using System.ComponentModel.DataAnnotations;

namespace HomeAssistantStateMaching.Models;

public class StateMachine
{
    public int Id { get; set; }
    
    public Guid Handle { get; set; }

    [MaxLength(255)]
    public string Name { get; set; } = null!;

    public bool Enabled { get; set; }

    public ICollection<HAClient> HAClients { get; set; } = null!;

    public ICollection<State> States { get; set; } = null!;

    public ICollection<Transition> Transitions { get; set; } = null!;
}
