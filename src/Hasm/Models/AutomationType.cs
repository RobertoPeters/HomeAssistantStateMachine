using System.ComponentModel.DataAnnotations;

namespace Hasm.Models;

public enum AutomationType: int
{
    [Display(Name="State Machine")]
    StateMachine = 0,

    [Display(Name = "Flow")]
    Flow = 1,
}
