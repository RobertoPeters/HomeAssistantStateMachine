using System.ComponentModel.DataAnnotations;

namespace HomeAssistantStateMachine.Models;

public class StateMachine
{
    public int Id { get; set; }
    
     [StringLength(255)]
    public string Name { get; set; } = null!;

    public bool Enabled { get; set; }

    public ICollection<HAClient> HAClients { get; set; } = [];

    public ICollection<State> States { get; set; } = [];

    public ICollection<Transition> Transitions { get; set; } = [];
}
