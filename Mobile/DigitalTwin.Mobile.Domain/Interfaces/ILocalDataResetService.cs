namespace DigitalTwin.Mobile.Domain.Interfaces;

/// <summary>
/// Clears local tables so cloud bootstrap can be applied with consistent IDs.
/// </summary>
public interface ILocalDataResetService
{
    Task ResetAllAsync();
}

