using System.Text.Json.Serialization;

namespace Hasm.Models;

public class Automation: ModelBase
{
    public string Name { get; set; } = null!;
    public bool Enabled { get; set; }
    public bool IsSubAutomation { get; set; }
    public AutomationType AutomationType { get; set; }
    public string Data { get; set; } = null!;
}
