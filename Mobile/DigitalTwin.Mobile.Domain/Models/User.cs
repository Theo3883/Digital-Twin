using DigitalTwin.Mobile.Domain.Enums;

namespace DigitalTwin.Mobile.Domain.Models;

/// <summary>
/// Mobile user entity - simplified for mobile app needs
/// </summary>
public class User
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? PhotoUrl { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? Country { get; set; }
    public DateTime? DateOfBirth { get; set; }
    
    // Mobile-specific properties
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public bool IsSynced { get; set; } = false;
}