using System.Security.Claims;
using DigitalTwin.Application.DTOs.MobileSync;
using DigitalTwin.Domain.Interfaces.Repositories;
using DigitalTwin.Domain.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DigitalTwin.WebAPI.Controllers;

/// <summary>
/// Mobile sync endpoints for User data.
/// </summary>
[ApiController]
[Route("api/mobile/sync/users")]
[Authorize(Roles = "Patient")]
public class MobileSyncUsersController : ControllerBase
{
    private readonly IUserRepository _users;
    private readonly ILogger<MobileSyncUsersController> _logger;
    private readonly Dictionary<string, DateTime> _processedRequests = new(); // Simple in-memory cache

    public MobileSyncUsersController(
        IUserRepository users,
        ILogger<MobileSyncUsersController> logger)
    {
        _users = users;
        _logger = logger;
    }

    private string CurrentUserEmail =>
        User.FindFirstValue(ClaimTypes.Email)
        ?? throw new UnauthorizedAccessException("Email claim missing.");

    /// <summary>
    /// Upserts user data to cloud storage.
    /// </summary>
    [HttpPost("upsert")]
    public async Task<ActionResult<UpsertUserResponse>> UpsertUser([FromBody] UpsertUserRequest request)
    {
        // Idempotency check
        var requestKey = $"{request.DeviceId}:{request.RequestId}";
        if (_processedRequests.ContainsKey(requestKey))
        {
            _logger.LogDebug("[UserSync] Duplicate request {RequestId} from device {DeviceId}", 
                request.RequestId, request.DeviceId);
            
            return Ok(new UpsertUserResponse
            {
                Success = true,
                RequestId = request.RequestId
            });
        }

        try
        {
            // Verify the user is updating their own data
            if (request.User.Email != CurrentUserEmail)
            {
                return Forbid("Users can only update their own data.");
            }

            // Find existing user by email (current drainer behavior)
            var existing = await _users.GetByEmailAsync(request.User.Email);
            Guid cloudUserId;

            if (existing is not null)
            {
                // Update existing user
                existing.FirstName = request.User.FirstName;
                existing.LastName = request.User.LastName;
                existing.PhotoUrl = request.User.PhotoUrl;
                existing.Phone = request.User.Phone;
                existing.Address = request.User.Address;
                existing.City = request.User.City;
                existing.Country = request.User.Country;
                existing.DateOfBirth = request.User.DateOfBirth;
                existing.UpdatedAt = DateTime.UtcNow;

                await _users.UpdateAsync(existing);
                cloudUserId = existing.Id;

                _logger.LogInformation("[UserSync] Updated user {Email} (CloudId={CloudId})", 
                    request.User.Email, cloudUserId);
            }
            else
            {
                // Create new user (should rarely happen since auth creates users)
                var newUser = new User
                {
                    Email = request.User.Email,
                    Role = request.User.Role,
                    FirstName = request.User.FirstName,
                    LastName = request.User.LastName,
                    PhotoUrl = request.User.PhotoUrl,
                    Phone = request.User.Phone,
                    Address = request.User.Address,
                    City = request.User.City,
                    Country = request.User.Country,
                    DateOfBirth = request.User.DateOfBirth,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                await _users.AddAsync(newUser);
                cloudUserId = newUser.Id;

                _logger.LogInformation("[UserSync] Created user {Email} (CloudId={CloudId})", 
                    request.User.Email, cloudUserId);
            }

            // Mark request as processed
            _processedRequests[requestKey] = DateTime.UtcNow;

            return Ok(new UpsertUserResponse
            {
                Success = true,
                CloudUserId = cloudUserId,
                RequestId = request.RequestId
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[UserSync] Failed to upsert user {Email}", request.User.Email);
            
            return Ok(new UpsertUserResponse
            {
                Success = false,
                ErrorMessage = "Failed to sync user data",
                RequestId = request.RequestId
            });
        }
    }
}