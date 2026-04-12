namespace DigitalTwin.Mobile.OCR.Models.Enums;

public enum SecurityMode
{
    /// <summary>Full enforcement: passcode required, biometric vault gating, no plaintext on disk.</summary>
    Strict,
    /// <summary>Relaxed enforcement for simulator and debug builds (clearly marked, not for release).</summary>
    RelaxedDebug
}
