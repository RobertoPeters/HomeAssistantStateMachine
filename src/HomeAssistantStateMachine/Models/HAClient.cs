using System.ComponentModel.DataAnnotations;

namespace HomeAssistantStateMachine.Models;

public class HAClient
{
    public int Id { get; set; }

    [StringLength(255)]
    public string Name { get; set; } = null!;
    
    public bool Enabled { get; set; }

    [StringLength(255)]
    public string Host { get; set; } = null!;

    [StringLength(255)]
    public string Token { get; set; } = null!;
}
