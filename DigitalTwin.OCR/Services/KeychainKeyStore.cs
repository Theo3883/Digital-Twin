using DigitalTwin.OCR.Models;
using Microsoft.Extensions.Logging;

namespace DigitalTwin.OCR.Services;

/// <summary>
/// Stores and retrieves the OCR vault master key from the iOS Keychain.
/// Uses SecAccessControl with biometric + device passcode gating.
/// Documents must NOT be stored here — only the 32-byte master key.
/// </summary>
public sealed class KeychainKeyStore
{
    private const string KeyLabel = "com.digitaltwin.ocr.masterkey";
    private const string KeyAccount = "ocr-vault-master";
    private const string KeyService = "DigitalTwin.OCR";

    private readonly ILogger<KeychainKeyStore> _logger;

    public KeychainKeyStore(ILogger<KeychainKeyStore> logger) => _logger = logger;

#if IOS || MACCATALYST
    public OcrResult<bool> StoreKey(byte[] keyBytes)
    {
        try
        {
            using var accessControl = new Security.SecAccessControl(
                Security.SecAccessible.WhenPasscodeSetThisDeviceOnly,
                Security.SecAccessControlCreateFlags.UserPresence);

            var record = new Security.SecRecord(Security.SecKind.GenericPassword)
            {
                Label = KeyLabel,
                Account = KeyAccount,
                Service = KeyService,
                Generic = Foundation.NSData.FromArray(keyBytes),
                ValueData = Foundation.NSData.FromArray(keyBytes),
                AccessControl = accessControl
            };

            // Remove any existing key first
            Security.SecKeyChain.Remove(new Security.SecRecord(Security.SecKind.GenericPassword)
            {
                Account = KeyAccount,
                Service = KeyService
            });

            var status = Security.SecKeyChain.Add(record);
            if (status != Security.SecStatusCode.Success)
            {
                _logger.LogWarning("[OCR Keychain] Add failed with status {Status}", status);
                return OcrResult<bool>.Fail($"Keychain add failed: {status}");
            }

            return OcrResult<bool>.Ok(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[OCR Keychain] StoreKey exception.");
            return OcrResult<bool>.Fail("Keychain store failed — see logs.");
        }
    }

    public OcrResult<byte[]> RetrieveKey()
    {
        try
        {
            var query = new Security.SecRecord(Security.SecKind.GenericPassword)
            {
                Account = KeyAccount,
                Service = KeyService,
                UseOperationPrompt = "Unlock your OCR vault"
            };

            var data = Security.SecKeyChain.QueryAsData(query, false, out var status);
            if (status != Security.SecStatusCode.Success || data is null)
            {
                _logger.LogWarning("[OCR Keychain] RetrieveKey failed with status {Status}", status);
                return OcrResult<byte[]>.Fail($"Keychain retrieve failed: {status}");
            }

            return OcrResult<byte[]>.Ok(data.ToArray());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[OCR Keychain] RetrieveKey exception.");
            return OcrResult<byte[]>.Fail("Keychain retrieve failed — see logs.");
        }
    }

    public bool KeyExists()
    {
        var query = new Security.SecRecord(Security.SecKind.GenericPassword)
        {
            Account = KeyAccount,
            Service = KeyService
        };
        Security.SecKeyChain.QueryAsData(query, false, out var status);
        return status == Security.SecStatusCode.Success;
    }

    public void DeleteKey()
    {
        Security.SecKeyChain.Remove(new Security.SecRecord(Security.SecKind.GenericPassword)
        {
            Account = KeyAccount,
            Service = KeyService
        });
    }
#else
    public OcrResult<bool> StoreKey(byte[] keyBytes)
        => OcrResult<bool>.Fail("Keychain is only available on iOS/macCatalyst.");

    public OcrResult<byte[]> RetrieveKey()
        => OcrResult<byte[]>.Fail("Keychain is only available on iOS/macCatalyst.");

    public bool KeyExists() => false;
    public void DeleteKey() { }
#endif
}
