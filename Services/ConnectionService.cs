using Microsoft.EntityFrameworkCore;
using StartupConnect.Data;
using StartupConnect.Models;
using StartupConnect.ViewModels;

namespace StartupConnect.Services;

/// <summary>
/// Member-to-member connections (requests, accept/decline/withdraw, remove) and the "My Network" page.
/// <see cref="AreConnectedAsync"/> is the check for features limited to connections (e.g. direct messages).
/// </summary>
public interface IConnectionService
{
    Task<ConnectionState> GetStateAsync(string viewerId, string otherUserId);
    Task<Dictionary<string, ConnectionState>> GetStatesAsync(string viewerId, IEnumerable<string> otherUserIds);
    Task<ServiceResult> SendAsync(string requesterId, string addresseeId, string? message);
    Task<ServiceResult> AcceptAsync(string userId, string requesterId);
    Task<ServiceResult> DeclineAsync(string userId, string requesterId);
    Task<ServiceResult> WithdrawAsync(string userId, string addresseeId);
    Task<ServiceResult> RemoveAsync(string userId, string otherUserId);
    Task<bool> AreConnectedAsync(string userId, string otherUserId);
    Task<List<string>> GetConnectedUserIdsAsync(string userId);
    Task<int> GetIncomingPendingCountAsync(string userId);
    Task<NetworkViewModel> GetNetworkAsync(string userId);
}

public sealed class ConnectionService : IConnectionService
{
    /// <summary>After a decline, the same person can't re-send for this long (the decliner can always send).</summary>
    public static readonly TimeSpan DeclineCooldown = TimeSpan.FromDays(30);
    public const int MaxRequestsPerDay = 25;

    private readonly ApplicationDbContext _db;
    private readonly INotificationService _notifications;
    private readonly IActivityService _activity;
    private readonly IPrivacyService _privacy;
    private readonly ILogger<ConnectionService> _logger;

    public ConnectionService(ApplicationDbContext db, INotificationService notifications, IActivityService activity,
        IPrivacyService privacy, ILogger<ConnectionService> logger)
    {
        _db = db;
        _notifications = notifications;
        _activity = activity;
        _privacy = privacy;
        _logger = logger;
    }

    private Task<Connection?> FindPairAsync(string x, string y)
    {
        var (a, b) = Connection.Order(x, y);
        return _db.Connections.FirstOrDefaultAsync(c => c.UserAId == a && c.UserBId == b);
    }

    private static ConnectionState StateFor(Connection? c, string viewerId) => c?.Status switch
    {
        ConnectionStatus.Accepted => ConnectionState.Connected,
        ConnectionStatus.Pending => c.RequesterId == viewerId ? ConnectionState.OutgoingPending : ConnectionState.IncomingPending,
        _ => ConnectionState.None
    };

    public async Task<ConnectionState> GetStateAsync(string viewerId, string otherUserId)
    {
        if (viewerId == otherUserId) return ConnectionState.Self;
        var (a, b) = Connection.Order(viewerId, otherUserId);
        var c = await _db.Connections.AsNoTracking().FirstOrDefaultAsync(x => x.UserAId == a && x.UserBId == b);
        return StateFor(c, viewerId);
    }

    public async Task<Dictionary<string, ConnectionState>> GetStatesAsync(string viewerId, IEnumerable<string> otherUserIds)
    {
        var ids = otherUserIds.Distinct().ToList();
        var rows = await _db.Connections.AsNoTracking()
            .Where(c => (c.RequesterId == viewerId && ids.Contains(c.AddresseeId)) || (c.AddresseeId == viewerId && ids.Contains(c.RequesterId)))
            .ToListAsync();
        var map = ids.ToDictionary(id => id, id => id == viewerId ? ConnectionState.Self : ConnectionState.None);
        foreach (var c in rows) map[c.OtherUserId(viewerId)] = StateFor(c, viewerId);
        return map;
    }

    public async Task<ServiceResult> SendAsync(string requesterId, string addresseeId, string? message)
    {
        if (string.IsNullOrEmpty(addresseeId) || requesterId == addresseeId)
            return ServiceResult.Fail("You can't connect with yourself.");

        message = message?.Trim();
        if (string.IsNullOrEmpty(message)) message = null;
        if (message?.Length > Connection.MaxMessageLength)
            return ServiceResult.Fail($"Your note can be at most {Connection.MaxMessageLength} characters.");

        var target = await _db.Users.AsNoTracking()
            .Where(u => u.Id == addresseeId)
            .Select(u => new { u.FullName, u.IsActive })
            .FirstOrDefaultAsync();
        if (target == null || !target.IsActive) return ServiceResult.Fail("That member isn't available.");
        var targetName = string.IsNullOrWhiteSpace(target.FullName) ? "This member" : target.FullName;

        var now = DateTime.UtcNow;
        var existing = await FindPairAsync(requesterId, addresseeId);
        if (existing != null)
        {
            switch (existing.Status)
            {
                case ConnectionStatus.Accepted:
                    return ServiceResult.Fail($"You're already connected with {targetName}.");
                case ConnectionStatus.Pending when existing.RequesterId == requesterId:
                    return ServiceResult.Fail($"Your request to {targetName} is still pending.");
                case ConnectionStatus.Pending:
                    // They already asked us — sending back simply accepts their request.
                    return await AcceptAsync(requesterId, addresseeId);
                case ConnectionStatus.Declined when existing.RequesterId == requesterId
                                                   && existing.RespondedAt.HasValue
                                                   && existing.RespondedAt.Value.Add(DeclineCooldown) > now:
                    return ServiceResult.Fail($"{targetName} isn't accepting a request from you right now. You can try again after {existing.RespondedAt.Value.Add(DeclineCooldown):dd MMM yyyy}.");
            }
        }

        var sentToday = await _db.Connections.CountAsync(c => c.RequesterId == requesterId && c.UpdatedAt > now.AddDays(-1)
            && (c.Status == ConnectionStatus.Pending || c.Status == ConnectionStatus.Accepted || c.Status == ConnectionStatus.Declined));
        if (sentToday >= MaxRequestsPerDay)
            return ServiceResult.Fail("You've sent a lot of connection requests today. Please try again tomorrow.");

        if (existing == null)
        {
            existing = new Connection();
            _db.Connections.Add(existing);
        }
        existing.SetPair(requesterId, addresseeId);
        existing.Status = ConnectionStatus.Pending;
        existing.Message = message;
        existing.CreatedAt = now;
        existing.UpdatedAt = now;
        existing.RespondedAt = null;

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            // Both members clicked "Connect" at the same moment: the unique pair index rejects the second row.
            _logger.LogInformation(ex, "Concurrent connection request between {A} and {B}", requesterId, addresseeId);
            return ServiceResult.Fail("A request between you two already exists — refresh the page to see it.");
        }

        var requesterName = await _db.Users.Where(u => u.Id == requesterId).Select(u => u.FullName).FirstOrDefaultAsync() ?? "A member";
        await _notifications.CreateAsync(addresseeId, "New connection request",
            message == null
                ? $"{requesterName} wants to connect with you on StartupConnect."
                : $"{requesterName} wants to connect with you: \"{message}\"",
            "/Network?tab=incoming", NotificationCategory.Connection);

        return ServiceResult.Ok($"Connection request sent to {targetName}.");
    }

    public async Task<ServiceResult> AcceptAsync(string userId, string requesterId)
    {
        var c = await FindPairAsync(userId, requesterId);
        if (c == null || c.Status != ConnectionStatus.Pending || c.AddresseeId != userId)
            return ServiceResult.Fail("That request is no longer pending.");

        var now = DateTime.UtcNow;
        c.Status = ConnectionStatus.Accepted;
        c.RespondedAt = now;
        c.UpdatedAt = now;

        var names = await _db.Users.Where(u => u.Id == userId || u.Id == requesterId)
            .Select(u => new { u.Id, u.FullName }).ToDictionaryAsync(u => u.Id, u => u.FullName);
        var myName = names.GetValueOrDefault(userId) ?? "A member";
        var theirName = names.GetValueOrDefault(requesterId) ?? "a member";

        await _activity.RecordAsync(userId, ActivityTypes.Connected, $"You connected with {theirName}.", $"/Profile/Detail/{requesterId}", save: false);
        await _activity.RecordAsync(requesterId, ActivityTypes.Connected, $"You connected with {myName}.", $"/Profile/Detail/{userId}", save: false);
        await _db.SaveChangesAsync();

        await _notifications.CreateAsync(requesterId, "Connection accepted",
            $"{myName} accepted your connection request. You can now see each other's full profiles.",
            $"/Profile/Detail/{userId}", NotificationCategory.Connection);
        return ServiceResult.Ok($"You're now connected with {theirName}.");
    }

    public async Task<ServiceResult> DeclineAsync(string userId, string requesterId)
    {
        var c = await FindPairAsync(userId, requesterId);
        if (c == null || c.Status != ConnectionStatus.Pending || c.AddresseeId != userId)
            return ServiceResult.Fail("That request is no longer pending.");

        c.Status = ConnectionStatus.Declined;
        c.RespondedAt = DateTime.UtcNow;
        c.UpdatedAt = c.RespondedAt.Value;
        await _db.SaveChangesAsync();
        // Declines are silent: the requester isn't notified.
        return ServiceResult.Ok("Request declined.");
    }

    public async Task<ServiceResult> WithdrawAsync(string userId, string addresseeId)
    {
        var c = await FindPairAsync(userId, addresseeId);
        if (c == null || c.Status != ConnectionStatus.Pending || c.RequesterId != userId)
            return ServiceResult.Fail("That request is no longer pending.");

        c.Status = ConnectionStatus.Withdrawn;
        c.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return ServiceResult.Ok("Request withdrawn.");
    }

    public async Task<ServiceResult> RemoveAsync(string userId, string otherUserId)
    {
        var c = await FindPairAsync(userId, otherUserId);
        if (c == null || c.Status != ConnectionStatus.Accepted)
            return ServiceResult.Fail("You're not connected with this member.");

        _db.Connections.Remove(c);
        await _db.SaveChangesAsync();
        return ServiceResult.Ok("Connection removed.");
    }

    public async Task<bool> AreConnectedAsync(string userId, string otherUserId)
    {
        if (userId == otherUserId) return false;
        var (a, b) = Connection.Order(userId, otherUserId);
        return await _db.Connections.AnyAsync(c => c.UserAId == a && c.UserBId == b && c.Status == ConnectionStatus.Accepted);
    }

    public Task<List<string>> GetConnectedUserIdsAsync(string userId) =>
        _db.Connections.AsNoTracking()
            .Where(c => c.Status == ConnectionStatus.Accepted && (c.RequesterId == userId || c.AddresseeId == userId))
            .Select(c => c.RequesterId == userId ? c.AddresseeId : c.RequesterId)
            .ToListAsync();

    public Task<int> GetIncomingPendingCountAsync(string userId) =>
        _db.Connections.CountAsync(c => c.AddresseeId == userId && c.Status == ConnectionStatus.Pending && c.Requester.IsActive);

    private sealed record NetworkRow(int Id, ConnectionStatus Status, string RequesterId, string? Message, DateTime CreatedAt,
        DateTime? RespondedAt, string OtherId, string FullName, string? City, string? State, bool IsActive, string? PhotoUrl,
        bool IsInvestor, bool IsVerified, List<string> Skills);

    public async Task<NetworkViewModel> GetNetworkAsync(string userId)
    {
        var links = await _db.Connections.AsNoTracking()
            .Where(c => (c.RequesterId == userId || c.AddresseeId == userId)
                && (c.Status == ConnectionStatus.Pending || c.Status == ConnectionStatus.Accepted))
            .Select(c => new { c.Id, c.Status, c.RequesterId, c.Message, c.CreatedAt, c.RespondedAt,
                OtherId = c.RequesterId == userId ? c.AddresseeId : c.RequesterId })
            .ToListAsync();
        var ids = links.Select(l => l.OtherId).Distinct().ToList();
        var people = await _db.Users.AsNoTracking()
            .Where(u => ids.Contains(u.Id))
            .Select(u => new
            {
                u.Id, u.FullName, u.City, u.State, u.IsActive,
                PhotoUrl = u.Profile != null ? u.Profile.ProfilePhotoUrl : null,
                IsInvestor = u.Profile != null && u.Profile.IsInvestor,
                IsVerified = u.Profile != null && u.Profile.IsVerifiedFounder,
                Skills = u.Profile != null ? u.Profile.Skills.Select(s => s.SkillName).ToList() : new List<string>()
            })
            .ToDictionaryAsync(u => u.Id);
        var rows = links.Where(l => people.ContainsKey(l.OtherId)).Select(l =>
        {
            var o = people[l.OtherId];
            return new NetworkRow(l.Id, l.Status, l.RequesterId, l.Message, l.CreatedAt, l.RespondedAt,
                o.Id, o.FullName, o.City, o.State, o.IsActive, o.PhotoUrl, o.IsInvestor, o.IsVerified, o.Skills);
        }).ToList();
        rows = rows.Where(r => r.IsActive).ToList();

        var otherIds = rows.Select(r => r.OtherId).ToList();
        var privacy = await _privacy.GetPrivacyAsync(otherIds);
        var viewable = await _privacy.GetViewableAsync(otherIds, userId, viewerIsAdmin: false);

        NetworkPersonViewModel Map(NetworkRow r, DateTime date)
        {
            var p = privacy.GetValueOrDefault(r.OtherId) ?? UserPrivacy.Default;
            var location = p.ShowLocation ? string.Join(", ", new[] { r.City, r.State }.Where(v => !string.IsNullOrWhiteSpace(v))) : null;
            return new NetworkPersonViewModel
            {
                ConnectionId = r.Id,
                UserId = r.OtherId,
                FullName = r.FullName,
                PhotoUrl = r.PhotoUrl,
                IsInvestor = r.IsInvestor,
                IsVerified = r.IsVerified,
                Location = string.IsNullOrEmpty(location) ? null : location,
                Skills = r.Skills,
                Message = r.Message,
                Date = date,
                CanViewProfile = viewable.Contains(r.OtherId)
            };
        }

        return new NetworkViewModel
        {
            Connections = rows.Where(r => r.Status == ConnectionStatus.Accepted)
                .Select(r => Map(r, r.RespondedAt ?? r.CreatedAt)).OrderBy(p => p.FullName).ToList(),
            Incoming = rows.Where(r => r.Status == ConnectionStatus.Pending && r.RequesterId != userId)
                .Select(r => Map(r, r.CreatedAt)).OrderByDescending(p => p.Date).ToList(),
            Sent = rows.Where(r => r.Status == ConnectionStatus.Pending && r.RequesterId == userId)
                .Select(r => Map(r, r.CreatedAt)).OrderByDescending(p => p.Date).ToList()
        };
    }
}
