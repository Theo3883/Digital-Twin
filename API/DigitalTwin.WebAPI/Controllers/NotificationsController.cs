using System.Security.Claims;
using DigitalTwin.Domain.Enums;
using DigitalTwin.Domain.Interfaces.Repositories;
using DigitalTwin.Domain.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DigitalTwin.WebAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(AuthenticationSchemes = "Bearer,Google")]
public class NotificationsController : ControllerBase
{
    private readonly IUserRepository _users;
    private readonly INotificationRepository _notifications;

    public NotificationsController(
        IUserRepository users,
        INotificationRepository notifications)
    {
        _users = users;
        _notifications = notifications;
    }

    private string UserEmail =>
        User.FindFirstValue(ClaimTypes.Email)
        ?? throw new UnauthorizedAccessException("Email claim missing.");

    private UserRole UserRole
    {
        get
        {
            var role = User.FindFirstValue(ClaimTypes.Role);
            return Enum.TryParse<UserRole>(role, ignoreCase: true, out var parsed)
                ? parsed
                : UserRole.Patient;
        }
    }

    public record NotificationDto(
        Guid Id,
        string Title,
        string Body,
        int Type,
        int Severity,
        Guid? PatientId,
        Guid? ActorUserId,
        string? ActorName,
        DateTime CreatedAt,
        DateTime? ReadAt);

    [HttpGet]
    public async Task<ActionResult<IEnumerable<NotificationDto>>> GetNotifications(
        [FromQuery] int limit = 50,
        [FromQuery] bool unreadOnly = false)
    {
        var user = await _users.GetByEmailAsync(UserEmail);
        if (user is null) return Unauthorized();

        var items = await _notifications.GetByRecipientAsync(user.Id, UserRole, unreadOnly, limit);
        return Ok(items.Select(ToDto));
    }

    [HttpGet("unread-count")]
    public async Task<ActionResult<object>> GetUnreadCount()
    {
        var user = await _users.GetByEmailAsync(UserEmail);
        if (user is null) return Unauthorized();

        var count = await _notifications.GetUnreadCountAsync(user.Id, UserRole);
        return Ok(new { count });
    }

    [HttpPost("{id:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid id)
    {
        var user = await _users.GetByEmailAsync(UserEmail);
        if (user is null) return Unauthorized();

        var item = await _notifications.GetByIdAsync(id, user.Id, UserRole);
        if (item is null) return NotFound();

        await _notifications.MarkReadAsync(id, DateTime.UtcNow);
        return NoContent();
    }

    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllRead()
    {
        var user = await _users.GetByEmailAsync(UserEmail);
        if (user is null) return Unauthorized();

        await _notifications.MarkAllReadAsync(user.Id, UserRole, DateTime.UtcNow);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var user = await _users.GetByEmailAsync(UserEmail);
        if (user is null) return Unauthorized();

        var item = await _notifications.GetByIdAsync(id, user.Id, UserRole);
        if (item is null) return NotFound();

        await _notifications.SoftDeleteAsync(id, DateTime.UtcNow);
        return NoContent();
    }

    private static NotificationDto ToDto(NotificationItem notification) => new(
        notification.Id,
        notification.Title,
        notification.Body,
        (int)notification.Type,
        (int)notification.Severity,
        notification.PatientId,
        notification.ActorUserId,
        notification.ActorName,
        notification.CreatedAt,
        notification.ReadAt);
}
