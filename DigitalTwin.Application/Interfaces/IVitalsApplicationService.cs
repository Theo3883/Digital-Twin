using DigitalTwin.Application.DTOs;
using DigitalTwin.Application.Enums;

namespace DigitalTwin.Application.Interfaces;

/// <summary>
/// Defines application operations for live and recent vital-sign data.
/// </summary>
public interface IVitalsApplicationService
{
    /// <summary>
    /// Gets the live vital-sign stream enriched with application DTO data.
    /// </summary>
    IObservable<VitalSignDto> GetLiveVitals();

    /// <summary>
    /// Gets the latest samples for the specified vital-sign type.
    /// </summary>
    Task<IEnumerable<VitalSignDto>> GetLatestSamplesAsync(VitalSignType type, int count = 20);
}
