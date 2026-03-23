using DigitalTwin.OCR.Models;
using Microsoft.Extensions.Logging;

namespace DigitalTwin.OCR.Services;

/// <summary>
/// Evaluates biometric / passcode authentication via LocalAuthentication.LAContext.
/// </summary>
public sealed class LocalAuthenticationService
{
    private readonly ILogger<LocalAuthenticationService> _logger;

    public LocalAuthenticationService(ILogger<LocalAuthenticationService> logger) => _logger = logger;

#if IOS || MACCATALYST
    public async Task<OcrResult<bool>> AuthenticateAsync(string reason, CancellationToken ct = default)
    {
        try
        {
            using var context = new LocalAuthentication.LAContext();
            // DeviceOwnerAuthentication allows passcode fallback when Face ID / Touch ID
            // is unavailable (e.g. simulator, un-enrolled device).
            var policy = context.CanEvaluatePolicy(
                LocalAuthentication.LAPolicy.DeviceOwnerAuthenticationWithBiometrics, out _)
                ? LocalAuthentication.LAPolicy.DeviceOwnerAuthenticationWithBiometrics
                : LocalAuthentication.LAPolicy.DeviceOwnerAuthentication;

            var (success, error) = await context.EvaluatePolicyAsync(policy, reason);

            if (!success)
            {
                var msg = error?.LocalizedDescription ?? "Authentication failed.";
                _logger.LogWarning("[OCR Auth] Biometric auth failed: [redacted]");
                return OcrResult<bool>.Fail(msg);
            }

            return OcrResult<bool>.Ok(true);
        }
        catch (OperationCanceledException)
        {
            return OcrResult<bool>.Fail("Authentication was cancelled.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[OCR Auth] EvaluatePolicyAsync exception.");
            return OcrResult<bool>.Fail("Authentication error — see logs.");
        }
    }
#else
    public Task<OcrResult<bool>> AuthenticateAsync(string reason, CancellationToken ct = default)
        => Task.FromResult(OcrResult<bool>.Ok(true)); // pass-through on simulator / non-iOS
#endif
}
