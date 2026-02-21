using DigitalTwin.Application.DTOs;

namespace DigitalTwin.Application.Interfaces;

public interface IAuthApplicationService
{
    Task<AuthResultDto> SignInWithGoogleAsync();
    Task SignOutAsync();
    Task<AuthResultDto?> GetCurrentUserAsync();
    Task<long?> GetCurrentPatientIdAsync();
}
