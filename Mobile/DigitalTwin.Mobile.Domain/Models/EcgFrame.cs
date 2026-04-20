namespace DigitalTwin.Mobile.Domain.Models;

public class EcgFrame
{
    public double[] Samples { get; set; } = [];

    /// <summary>Number of leads encoded in Samples (1 for domain-only, 12 for full 12-lead).</summary>
    public int NumLeads { get; set; } = 1;

    public double SpO2 { get; set; }
    public int HeartRate { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Probabilities from on-device CoreML inference (ECGClassifier.mlpackage).
    /// Keys: "1dAVb", "RBBB", "LBBB", "SB", "AF", "ST". Null when not yet computed.
    /// </summary>
    public Dictionary<string, double>? MlScores { get; set; }
}
