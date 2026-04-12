namespace DigitalTwin.Mobile.Domain.Interfaces;

/// <summary>
/// Clears local tables so cloud bootstrap can be applied with consistent IDs.
/// </summary>
public interface ILocalDataResetService
{
    Task ResetAllAsync();

    /// <summary>
    /// Resets cloud-synced data (user, patient, vitals, etc.) but preserves
    /// OCR documents and medical history entries which have local vault bindings.
    /// </summary>
    Task ResetCloudSyncedDataAsync();
}

