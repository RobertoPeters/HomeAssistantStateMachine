using System.ComponentModel.DataAnnotations;

namespace Hasm.Models;

public enum ClientType: int
{
    [Display(Name="Home Assistant")]
    HomeAssistant = 0,

    [Display(Name = "MQTT")]
    Mqtt = 1,

    [Display(Name = "Generic")]
    Generic = 2,

    [Display(Name = "Timer")]
    Timer = 3,

    [Display(Name = "Sqlite database")]
    SqliteDatabase = 4
}
