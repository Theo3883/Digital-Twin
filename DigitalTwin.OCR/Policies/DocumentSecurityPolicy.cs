using DigitalTwin.OCR.Models;
using DigitalTwin.OCR.Models.Enums;

namespace DigitalTwin.OCR.Policies;

/// <summary>
/// Business rules for document security posture.
/// All methods are pure — no side effects, easy to test.
/// </summary>
public static class DocumentSecurityPolicy
{
    /// <summary>Returns true if the vault can be initialised given the current device security posture.</summary>
    public static bool CanInitializeVault(SecurityPosture posture)
    {
        if (posture.ActiveMode == SecurityMode.RelaxedDebug)
            return true;

        return posture.IsPasscodeSet;
    }

    /// <summary>
    /// Returns true if a document operation (scan, OCR, encrypt) can proceed.
    /// In Strict mode the vault must be unlocked.
    /// </summary>
    public static bool CanAccessDocument(SecurityPosture posture)
    {
        if (posture.ActiveMode == SecurityMode.RelaxedDebug)
            return posture.IsVaultInitialized;

        return posture.IsVaultInitialized && posture.IsVaultUnlocked;
    }

    /// <summary>
    /// Required security rows before showing Camera / Photos / Files actions.
    /// Face ID / biometry is <b>not</b> required (optional). All other rows must be satisfied:
    /// passcode (Strict), vault initialized, vault unlocked.
    /// RelaxedDebug: passcode is not enforced so simulator/dev flows can run without a device passcode.
    /// </summary>
    public static bool RequiredOcrRowsSatisfied(SecurityPosture posture)
    {
        if (!posture.IsVaultInitialized || !posture.IsVaultUnlocked)
            return false;

        if (posture.ActiveMode == SecurityMode.RelaxedDebug)
            return true;

        return posture.IsPasscodeSet;
    }

    /// <summary>Validates that the key material meets minimum requirements.</summary>
    public static (bool IsValid, string? Reason) ValidateMasterKey(ReadOnlySpan<byte> keyBytes)
    {
        if (keyBytes.Length != 32)
            return (false, $"Master key must be exactly 256 bits (32 bytes), got {keyBytes.Length * 8} bits.");

        // Reject all-zero key
        var allZero = true;
        foreach (var b in keyBytes)
            if (b != 0) { allZero = false; break; }

        if (allZero)
            return (false, "Master key is all-zero — generation failure.");

        return (true, null);
    }
}
