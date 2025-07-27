namespace Hasm.Services;
public class LogEntry
{
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public int AutomationId { get; set; }
    public string Message { get; set; } = null!;
}
