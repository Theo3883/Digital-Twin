namespace DigitalTwin.Application.DTOs;

/// <summary>
/// Data collected from the user during registration.
/// Includes fields Google may or may not provide (first/last name)
/// and fields Google never provides (phone, address, etc.).
/// </summary>
public class ProfileCompletionDto
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? Country { get; set; }
    public DateTime? DateOfBirth { get; set; }
}
