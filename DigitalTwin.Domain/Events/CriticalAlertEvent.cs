namespace DigitalTwin.Domain.Events;

public class CriticalAlertEvent
{
    public string RuleName { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
