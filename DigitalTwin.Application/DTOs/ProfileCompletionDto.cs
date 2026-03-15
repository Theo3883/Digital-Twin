namespace DigitalTwin.Application.DTOs;

/// <summary>
/// Represents the profile fields collected to complete registration.
/// </summary>
public class ProfileCompletionDto
{
    /// <summary>
    /// Gets or sets the user's first name.
    /// </summary>
    public string FirstName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the user's last name.
    /// </summary>
    public string LastName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the user's phone number.
    /// </summary>
    public string? Phone { get; set; }

    /// <summary>
    /// Gets or sets the user's address.
    /// </summary>
    public string? Address { get; set; }

    /// <summary>
    /// Gets or sets the user's city.
    /// </summary>
    public string? City { get; set; }

    /// <summary>
    /// Gets or sets the user's country.
    /// </summary>
    public string? Country { get; set; }

    /// <summary>
    /// Gets or sets the user's date of birth.
    /// </summary>
    public DateTime? DateOfBirth { get; set; }
}
