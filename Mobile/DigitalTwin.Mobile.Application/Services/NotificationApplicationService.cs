using DigitalTwin.Mobile.Domain.Interfaces;
using DigitalTwin.Mobile.Domain.Models;
using Microsoft.Extensions.Logging;

namespace DigitalTwin.Mobile.Application.Services;

public class NotificationApplicationService
{
    private readonly ICloudSyncService _cloudSync;
    private readonly IUserRepository _userRepository;
    private readonly ILogger<NotificationApplicationService> _logger;

    public NotificationApplicationService(
        ICloudSyncService cloudSync,
        IUserRepository userRepository,
        ILogger<NotificationApplicationService> logger)
    {
        _cloudSync = cloudSync;
        _userRepository = userRepository;
        _logger = logger;
    }

    public async Task<NotificationItem[]> GetNotificationsAsync(int limit = 50, bool unreadOnly = false)
    {
        try
        {
            var currentUser = await _userRepository.GetCurrentUserAsync();
            if (currentUser == null)
            {
                _logger.LogDebug("[Notifications] No current user in local DB; returning empty list.");
                return [];
            }

            if (!_cloudSync.IsAuthenticated)
            {
                _logger.LogDebug("[Notifications] Cloud auth not ready; returning empty notifications.");
                return [];
            }

            var notifications = await _cloudSync.GetNotificationsAsync(limit, unreadOnly);
            return notifications.ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Notifications] Failed to get notifications");
            return [];
        }
    }

    public async Task<int> GetUnreadCountAsync()
    {
        try
        {
            var currentUser = await _userRepository.GetCurrentUserAsync();
            if (currentUser == null)
            {
                _logger.LogDebug("[Notifications] No current user; returning 0.");
                return 0;
            }

            if (!_cloudSync.IsAuthenticated)
            {
                _logger.LogDebug("[Notifications] Cloud auth not ready; returning 0.");
                return 0;
            }

            return await _cloudSync.GetUnreadNotificationCountAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Notifications] Failed to get unread count");
            return 0;
        }
    }
}
