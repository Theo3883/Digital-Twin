using DigitalTwin.OCR.Models;
using DigitalTwin.OCR.Models.Enums;
using DigitalTwin.OCR.Policies;

namespace DigitalTwin.OCR.Tests;

public class DocumentSecurityPolicyTests
{
    private static SecurityPosture StrictPosture(
        bool passcode = true, bool biometry = true, bool initialized = true, bool unlocked = true)
        => new(passcode, biometry, "Face ID", initialized, unlocked, SecurityMode.Strict);

    private static SecurityPosture RelaxedPosture(
        bool initialized = true, bool unlocked = true)
        => new(false, false, "None", initialized, unlocked, SecurityMode.RelaxedDebug);

    // ── CanInitializeVault ───────────────────────────────────────────────────

    [Fact]
    public void CanInitializeVault_Strict_WithPasscode_ReturnsTrue()
        => Assert.True(DocumentSecurityPolicy.CanInitializeVault(StrictPosture(passcode: true)));

    [Fact]
    public void CanInitializeVault_Strict_NoPasscode_ReturnsFalse()
        => Assert.False(DocumentSecurityPolicy.CanInitializeVault(StrictPosture(passcode: false)));

    [Fact]
    public void CanInitializeVault_RelaxedDebug_NoPasscode_ReturnsTrue()
        => Assert.True(DocumentSecurityPolicy.CanInitializeVault(RelaxedPosture()));

    // ── CanAccessDocument ────────────────────────────────────────────────────

    [Fact]
    public void CanAccessDocument_Strict_InitializedAndUnlocked_ReturnsTrue()
        => Assert.True(DocumentSecurityPolicy.CanAccessDocument(StrictPosture(initialized: true, unlocked: true)));

    [Fact]
    public void CanAccessDocument_Strict_InitializedButLocked_ReturnsFalse()
        => Assert.False(DocumentSecurityPolicy.CanAccessDocument(StrictPosture(initialized: true, unlocked: false)));

    [Fact]
    public void CanAccessDocument_Strict_NotInitialized_ReturnsFalse()
        => Assert.False(DocumentSecurityPolicy.CanAccessDocument(StrictPosture(initialized: false, unlocked: false)));

    [Fact]
    public void CanAccessDocument_Relaxed_InitializedOnly_ReturnsTrue()
        => Assert.True(DocumentSecurityPolicy.CanAccessDocument(RelaxedPosture(initialized: true, unlocked: false)));

    // ── RequiredOcrRowsSatisfied (Face ID optional) ─────────────────────────────

    [Fact]
    public void RequiredOcrRowsStrict_AllRequiredGreen_ReturnsTrue()
        => Assert.True(DocumentSecurityPolicy.RequiredOcrRowsSatisfied(StrictPosture(
            passcode: true, biometry: true, initialized: true, unlocked: true)));

    [Fact]
    public void RequiredOcrRowsStrict_BiometryRed_PasscodeOk_ReturnsTrue()
        => Assert.True(DocumentSecurityPolicy.RequiredOcrRowsSatisfied(StrictPosture(
            passcode: true, biometry: false, initialized: true, unlocked: true)));

    [Fact]
    public void RequiredOcrRowsStrict_NoPasscode_ReturnsFalse()
        => Assert.False(DocumentSecurityPolicy.RequiredOcrRowsSatisfied(StrictPosture(
            passcode: false, biometry: true, initialized: true, unlocked: true)));

    [Fact]
    public void RequiredOcrRowsStrict_VaultLocked_ReturnsFalse()
        => Assert.False(DocumentSecurityPolicy.RequiredOcrRowsSatisfied(StrictPosture(
            passcode: true, biometry: true, initialized: true, unlocked: false)));

    [Fact]
    public void RequiredOcrRowsRelaxed_NoPasscode_InitUnlock_ReturnsTrue()
        => Assert.True(DocumentSecurityPolicy.RequiredOcrRowsSatisfied(RelaxedPosture(initialized: true, unlocked: true)));

    [Fact]
    public void RequiredOcrRowsRelaxed_VaultLocked_ReturnsFalse()
        => Assert.False(DocumentSecurityPolicy.RequiredOcrRowsSatisfied(RelaxedPosture(initialized: true, unlocked: false)));

    // ── ValidateMasterKey ────────────────────────────────────────────────────

    [Fact]
    public void ValidateMasterKey_32Bytes_ReturnsValid()
    {
        var key = new byte[32];
        key[0] = 1; // not all-zero
        var (isValid, reason) = DocumentSecurityPolicy.ValidateMasterKey(key);
        Assert.True(isValid);
        Assert.Null(reason);
    }

    [Fact]
    public void ValidateMasterKey_AllZero_ReturnsFail()
    {
        var (isValid, reason) = DocumentSecurityPolicy.ValidateMasterKey(new byte[32]);
        Assert.False(isValid);
        Assert.Contains("all-zero", reason!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateMasterKey_WrongLength_ReturnsFail()
    {
        var (isValid, reason) = DocumentSecurityPolicy.ValidateMasterKey(new byte[16]);
        Assert.False(isValid);
        Assert.Contains("256 bits", reason!);
    }
}
