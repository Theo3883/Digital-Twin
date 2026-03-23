using DigitalTwin.OCR.Models.Enums;

namespace DigitalTwin.OCR.Models;

public record SecurityPosture(
    bool IsPasscodeSet,
    bool IsBiometryAvailable,
    string BiometryType,
    bool IsVaultInitialized,
    bool IsVaultUnlocked,
    SecurityMode ActiveMode);
