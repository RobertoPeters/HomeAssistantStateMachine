using System.ComponentModel.DataAnnotations;

namespace HomeAssistantStateMachine.Models;

public class StateMachineHAClient
{
    public int Id { get; set; }

    public int HAClientId { get; set; }
    public HAClient? HAClient { get; set; }

    public int StateMachineId { get; set; }
    public StateMachine? StateMachine { get; set; }
}
