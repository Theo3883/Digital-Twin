namespace DigitalTwin.OCR.Models.Enums;

public enum SecurityMode
{
    /// <summary>Full enforcement: passcode required, biometric vault gating, no plaintext on disk.</summary>
    Strict,

    /// <summary>
    /// Relaxed enforcement for simulator and debug builds.
    /// Clearly marked — release builds must never use this.
    /// </summary>
    RelaxedDebug
}
