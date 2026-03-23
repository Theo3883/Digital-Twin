using DigitalTwin.OCR.Models;
using DigitalTwin.OCR.Models.Enums;
using Microsoft.Extensions.Logging;

namespace DigitalTwin.OCR.Services;

/// <summary>
/// Checks device security posture (passcode, biometry) using LocalAuthentication.
/// Non-throwing — all failures return an OcrResult.
/// </summary>
public sealed class SecurityService
{
    private readonly ILogger<SecurityService> _logger;

    public SecurityService(ILogger<SecurityService> logger) => _logger = logger;

#if IOS || MACCATALYST
    public SecurityPosture GetPosture(SecurityMode mode, bool isVaultInitialized, bool isVaultUnlocked)
    {
        using var context = new LocalAuthentication.LAContext();

        var canPasscode = context.CanEvaluatePolicy(
            LocalAuthentication.LAPolicy.DeviceOwnerAuthentication, out _);

        var canBiometry = context.CanEvaluatePolicy(
            LocalAuthentication.LAPolicy.DeviceOwnerAuthenticationWithBiometrics, out _);

        var biometryType = context.BiometryType switch
        {
            LocalAuthentication.LABiometryType.FaceId => "Face ID",
            LocalAuthentication.LABiometryType.TouchId => "Touch ID",
            LocalAuthentication.LABiometryType.OpticId => "Optic ID",
            _ => "None"
        };

        return new SecurityPosture(
            IsPasscodeSet: canPasscode,
            IsBiometryAvailable: canBiometry,
            BiometryType: biometryType,
            IsVaultInitialized: isVaultInitialized,
            IsVaultUnlocked: isVaultUnlocked,
            ActiveMode: mode);
    }
#else
    public SecurityPosture GetPosture(SecurityMode mode, bool isVaultInitialized, bool isVaultUnlocked)
        => new(
            IsPasscodeSet: mode == SecurityMode.RelaxedDebug,
            IsBiometryAvailable: false,
            BiometryType: "None",
            IsVaultInitialized: isVaultInitialized,
            IsVaultUnlocked: isVaultUnlocked,
            ActiveMode: mode);
#endif
}
