using DigitalTwin.Mobile.OCR.Models.Enums;

namespace DigitalTwin.Mobile.OCR.Models;

public record SecurityPosture(
    bool IsPasscodeSet,
    bool IsBiometryAvailable,
    string BiometryType,
    bool IsVaultInitialized,
    bool IsVaultUnlocked,
    SecurityMode ActiveMode);
