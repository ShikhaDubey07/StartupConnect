using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using StartupConnect.Data;
using StartupConnect.Hubs;
using StartupConnect.Infrastructure;
using StartupConnect.Models;
using StartupConnect.Services.Email;

namespace StartupConnect.Services;

/// <summary>
/// How a user's <see cref="UserSettings"/> apply to a notification:
/// <list type="bullet">
/// <item>In-app (bell, toast, notification centre) only when <see cref="UserSettings.InAppNotifications"/> is on.</item>
/// <item>Email only when <see cref="UserSettings.EmailNotifications"/> is on <b>and</b> the category is enabled:
/// matches → NotifyOnMatches; interest / connection / message requests → NotifyOnMessages;
/// likes are never emailed; everything else (comments, team, challenges, moderation, account) is always emailed.</item>
/// </list>
/// Transactional account emails (confirmation, password reset, password changed) are not notifications and always send.
/// </summary>
public static class NotificationPreferences
{
    public static bool InAppAllowed(UserSettings settings) => settings.InAppNotifications;

    public static bool EmailAllowed(UserSettings settings, NotificationCategory category) =>
        settings.EmailNotifications && category switch
        {
            NotificationCategory.Like => false,
            NotificationCategory.Match => settings.NotifyOnMatches,
            NotificationCategory.Interest or NotificationCategory.Connection or NotificationCategory.Message => settings.NotifyOnMessages,
            _ => true
        };

    public static (string Icon, string Color) Style(NotificationCategory category) => category switch
    {
        NotificationCategory.Like => ("bi-heart-fill", "danger"),
        NotificationCategory.Comment => ("bi-chat-dots-fill", "info"),
        NotificationCategory.Interest => ("bi-briefcase-fill", "primary"),
        NotificationCategory.Connection => ("bi-person-plus-fill", "primary"),
        NotificationCategory.Message => ("bi-envelope-fill", "primary"),
        NotificationCategory.Team => ("bi-people-fill", "info"),
        NotificationCategory.Challenge => ("bi-trophy-fill", "warning"),
        NotificationCategory.Moderation => ("bi-shield-check", "success"),
        NotificationCategory.Match => ("bi-lightning-charge-fill", "warning"),
        _ => ("bi-bell-fill", "secondary")
    };
}

public interface INotificationService
{
    /// <summary>
    /// Notifies a user, honouring their preferences (see <see cref="NotificationPreferences"/>). The in-app
    /// notification is stored and pushed live; the email copy is sent from the background queue.
    /// Also saves any pending changes on the shared DbContext (callers rely on this).
    /// </summary>
    Task CreateAsync(string userId, string title, string message, string? linkUrl = null,
        NotificationCategory category = NotificationCategory.System);
    Task<List<Notification>> GetUnreadAsync(string userId);
    Task<List<Notification>> GetAllAsync(string userId);
    Task MarkAsReadAsync(int id, string userId);
    Task MarkAllAsReadAsync(string userId);
    Task<int> GetUnreadCountAsync(string userId);
}

public class NotificationService : INotificationService
{
    private const int MaxTitle = 200;
    private const int MaxMessage = 1000;

    private readonly ApplicationDbContext _context;
    private readonly IHubContext<NotificationHub> _hubContext;
    private readonly IEmailQueue _emailQueue;
    private readonly IAppUrls _urls;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(ApplicationDbContext context, IHubContext<NotificationHub> hubContext,
        IEmailQueue emailQueue, IAppUrls urls, ILogger<NotificationService> logger)
    {
        _context = context;
        _hubContext = hubContext;
        _emailQueue = emailQueue;
        _urls = urls;
        _logger = logger;
    }

    public async Task CreateAsync(string userId, string title, string message, string? linkUrl = null,
        NotificationCategory category = NotificationCategory.System)
    {
        title = Truncate(title, MaxTitle);
        message = Truncate(message, MaxMessage);
        if (!UrlSafety.IsLocalPath(linkUrl)) linkUrl = null;

        var recipient = await _context.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new { u.Email, u.FullName, u.EmailConfirmed, u.IsActive, u.Settings })
            .FirstOrDefaultAsync();
        if (recipient == null)
        {
            await _context.SaveChangesAsync();
            return;
        }
        var settings = recipient.Settings ?? new UserSettings();

        Notification? notification = null;
        if (NotificationPreferences.InAppAllowed(settings))
        {
            notification = new Notification
            {
                UserId = userId,
                Title = title,
                Message = message,
                LinkUrl = linkUrl,
                Category = category,
                CreatedAt = DateTime.UtcNow
            };
            _context.Notifications.Add(notification);
        }
        await _context.SaveChangesAsync();

        if (notification != null)
        {
            try
            {
                await _hubContext.Clients.User(userId).SendAsync("ReceiveNotification", new
                {
                    id = notification.Id,
                    title = notification.Title,
                    message = notification.Message,
                    linkUrl = notification.LinkUrl,
                    category = notification.Category.ToString(),
                    createdAt = notification.CreatedAt
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not push notification {NotificationId} to user {UserId}", notification.Id, userId);
            }
        }

        if (recipient.IsActive && recipient.EmailConfirmed && !string.IsNullOrEmpty(recipient.Email)
            && NotificationPreferences.EmailAllowed(settings, category))
        {
            var email = EmailTemplates.Notification(recipient.Email, recipient.FullName, title, message,
                _urls.Absolute(linkUrl ?? "/Notifications"), _urls.ManagePreferencesUrl);
            await _emailQueue.QueueAsync(email, $"notification:{category}");
        }
    }

    public async Task<List<Notification>> GetUnreadAsync(string userId)
    {
        return await _context.Notifications
            .Where(n => n.UserId == userId && !n.IsRead)
            .OrderByDescending(n => n.CreatedAt)
            .Take(10)
            .ToListAsync();
    }

    public async Task<List<Notification>> GetAllAsync(string userId)
    {
        return await _context.Notifications
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .Take(50)
            .ToListAsync();
    }

    public async Task MarkAsReadAsync(int id, string userId)
    {
        var n = await _context.Notifications.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);
        if (n != null) { n.IsRead = true; await _context.SaveChangesAsync(); }
    }

    public async Task MarkAllAsReadAsync(string userId)
    {
        await _context.Notifications
            .Where(n => n.UserId == userId && !n.IsRead)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true));
    }

    public async Task<int> GetUnreadCountAsync(string userId)
    {
        return await _context.Notifications.CountAsync(n => n.UserId == userId && !n.IsRead);
    }

    private static string Truncate(string? value, int max)
    {
        value ??= string.Empty;
        return value.Length <= max ? value : value[..(max - 1)] + "…";
    }
}
