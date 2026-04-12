using DigitalTwin.Mobile.OCR.Models;
using DigitalTwin.Mobile.OCR.Models.Enums;

namespace DigitalTwin.Mobile.OCR.Policies;

/// <summary>
/// Business rules for document security posture.
/// All methods are pure — no side effects, easy to test.
/// </summary>
public static class DocumentSecurityPolicy
{
    public static bool CanInitializeVault(SecurityPosture posture)
    {
        if (posture.ActiveMode == SecurityMode.RelaxedDebug)
            return true;

        return posture.IsPasscodeSet;
    }

    public static bool CanAccessDocument(SecurityPosture posture)
    {
        if (posture.ActiveMode == SecurityMode.RelaxedDebug)
            return posture.IsVaultInitialized;

        return posture.IsVaultInitialized && posture.IsVaultUnlocked;
    }

    public static bool RequiredOcrRowsSatisfied(SecurityPosture posture)
    {
        if (!posture.IsVaultInitialized || !posture.IsVaultUnlocked)
            return false;

        if (posture.ActiveMode == SecurityMode.RelaxedDebug)
            return true;

        return posture.IsPasscodeSet;
    }

    public static (bool IsValid, string? Reason) ValidateMasterKey(ReadOnlySpan<byte> keyBytes)
    {
        if (keyBytes.Length != 32)
            return (false, $"Master key must be exactly 256 bits (32 bytes), got {keyBytes.Length * 8} bits.");

        var allZero = true;
        foreach (var b in keyBytes)
            if (b != 0) { allZero = false; break; }

        if (allZero)
            return (false, "Master key is all-zero — generation failure.");

        return (true, null);
    }
}
