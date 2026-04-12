namespace DigitalTwin.Mobile.Application.DTOs;

public record AuthenticationResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public string? AccessToken { get; init; }
    public UserDto? User { get; init; }
    public bool HasCloudProfile { get; init; }
}

public record UserDto
{
    public Guid Id { get; init; }
    public string Email { get; init; } = string.Empty;
    public int Role { get; init; }
    public string? FirstName { get; init; }
    public string? LastName { get; init; }
    public string? PhotoUrl { get; init; }
    public string? Phone { get; init; }
    public string? Address { get; init; }
    public string? City { get; init; }
    public string? Country { get; init; }
    public DateTime? DateOfBirth { get; init; }
    public DateTime? CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
}