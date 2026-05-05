using DigitalTwin.Mobile.Domain.Enums;

namespace DigitalTwin.Mobile.Domain.Models;

public class CriticalAlertEvent
{
    public string RuleName { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
