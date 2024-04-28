using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HomeAssistantStateMachine.Models;

public class Variable
{
    public int Id { get; set; }
    
    [StringLength(255)]
    public string Name { get; set; } = null!;

    public int? HAClientId { get; set; }
    public HAClient? HAClient { get; set; }

    public int? MqttClientId { get; set; }
    public MqttClient? MqttClient { get; set; }

    public int? StateMachineId { get; set; }
    public StateMachine? StateMachine { get; set; }

    public int? StateId { get; set; }
    public State? State { get; set; }

    public string? Data { get; set; }
}
