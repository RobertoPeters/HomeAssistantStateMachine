using System.ComponentModel.DataAnnotations;

namespace HomeAssistantStateMachine.Models;

public class HAClient
{
    public int Id { get; set; }
    
    public Guid Handle { get; set; }

    [MaxLength(255)]
    public string Name { get; set; } = null!;
    
    public bool Enabled { get; set; }

    [MaxLength(255)]
    public string Host { get; set; } = null!;

    [MaxLength(255)]
    public string Token { get; set; } = null!;
}
