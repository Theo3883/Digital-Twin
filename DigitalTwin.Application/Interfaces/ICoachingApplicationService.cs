using DigitalTwin.Application.DTOs;

namespace DigitalTwin.Application.Interfaces;

public interface ICoachingApplicationService
{
    Task<CoachingAdviceDto> GetAdviceAsync(CancellationToken ct = default);
}
