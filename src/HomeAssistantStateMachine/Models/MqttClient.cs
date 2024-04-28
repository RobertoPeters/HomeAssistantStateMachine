using System.ComponentModel.DataAnnotations;

namespace HomeAssistantStateMachine.Models;

public class MqttClient
{
    public int Id { get; set; }

    [StringLength(255)]
    public string Name { get; set; } = null!;
    
    public bool Enabled { get; set; }

    [StringLength(255)]
    public string Host { get; set; } = null!;

    public bool Tls { get; set; }

    public bool WebSocket { get; set; }

    [StringLength(255)]
    public string? Username { get; set; } = null!;

    [StringLength(255)]
    public string? Password { get; set; } = null!;
}
