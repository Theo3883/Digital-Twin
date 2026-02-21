using IOSHealthApp.Domain.Enums;

namespace IOSHealthApp.Domain.Models;

public class VitalSign
{
    public VitalSignType Type { get; set; }
    public double Value { get; set; }
    public string Unit { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
