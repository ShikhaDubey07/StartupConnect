using Microsoft.EntityFrameworkCore;
using StartupConnect.Data;
using StartupConnect.Models;

namespace StartupConnect.Services;

/// <summary>A user's privacy choices (defaults apply when they never saved settings).</summary>
public sealed record UserPrivacy(string Visibility, bool ShowEmail, bool ShowLocation, bool ShowAge)
{
    public static readonly UserPrivacy Default = From(null);

    public static UserPrivacy From(UserSettings? s) => new(
        NormaliseVisibility(s?.ProfileVisibility),
        s?.ShowEmail ?? false,
        s?.ShowLocation ?? true,
        s?.ShowAge ?? false);

    public bool IsPrivate => Visibility == ProfileVisibilityOptions.Private;

    private static string NormaliseVisibility(string? v) => v switch
    {
        ProfileVisibilityOptions.Private => ProfileVisibilityOptions.Private,
        ProfileVisibilityOptions.RegisteredUsers => ProfileVisibilityOptions.RegisteredUsers,
        _ => ProfileVisibilityOptions.Public
    };
}

public enum ProfileDenialReason { None, SignInRequired, Private }

/// <summary>What one viewer may see of one member's profile.</summary>
public sealed class ProfileAccess
{
    public bool CanView { get; init; }
    public ProfileDenialReason Reason { get; init; }
    public bool IsOwner { get; init; }
    public bool IsAdmin { get; init; }
    /// <summary>Private profile visible because the viewer is an accepted connection or teammate.</summary>
    public bool ViaRelationship { get; init; }
    public UserPrivacy Privacy { get; init; } = UserPrivacy.Default;

    // Effective field visibility for this viewer (owners and admins always see everything).
    public bool ShowEmail => IsOwner || IsAdmin || Privacy.ShowEmail;
    public bool ShowLocation => IsOwner || IsAdmin || Privacy.ShowLocation;
    public bool ShowAge => IsOwner || IsAdmin || Privacy.ShowAge;
}

/// <summary>
/// Single source of truth for profile privacy:
/// <list type="bullet">
/// <item><b>Public</b> — anyone, including signed-out visitors.</item>
/// <item><b>RegisteredUsers</b> — any signed-in member.</item>
/// <item><b>Private</b> — only the member, admins, accepted connections and teammates (people who share a team);
/// Private members are also left out of discovery lists (Smart Matches, Find Team, investor lists, public API).</item>
/// </list>
/// ShowEmail / ShowLocation / ShowAge apply to everyone except the member themself and admins.
/// Ideas remain public work: a submitter's name is shown on their approved ideas, but not their location
/// unless ShowLocation is on, and profile links go through <see cref="GetAccessAsync"/>.
/// </summary>
public interface IPrivacyService
{
    Task<ProfileAccess> GetAccessAsync(string targetUserId, string? viewerId, bool viewerIsAdmin);
    Task<Dictionary<string, UserPrivacy>> GetPrivacyAsync(IEnumerable<string> userIds);
    /// <summary>Of <paramref name="userIds"/>, those whose full profile <paramref name="viewerId"/> may open.</summary>
    Task<HashSet<string>> GetViewableAsync(IEnumerable<string> userIds, string? viewerId, bool viewerIsAdmin);
}

public static class PrivacyQueryExtensions
{
    /// <summary>Profiles that may appear in discovery lists (not Private, active accounts).</summary>
    public static IQueryable<UserProfile> WhereDiscoverable(this IQueryable<UserProfile> query) =>
        query.Where(p => p.User.IsActive
            && (p.User.Settings == null || p.User.Settings.ProfileVisibility != ProfileVisibilityOptions.Private));
}

public sealed class PrivacyService : IPrivacyService
{
    private readonly ApplicationDbContext _db;

    public PrivacyService(ApplicationDbContext db) => _db = db;

    public async Task<ProfileAccess> GetAccessAsync(string targetUserId, string? viewerId, bool viewerIsAdmin)
    {
        var settings = await _db.UserSettings.AsNoTracking().FirstOrDefaultAsync(s => s.UserId == targetUserId);
        var privacy = UserPrivacy.From(settings);
        var isOwner = viewerId != null && viewerId == targetUserId;

        if (isOwner || viewerIsAdmin)
            return new ProfileAccess { CanView = true, IsOwner = isOwner, IsAdmin = viewerIsAdmin && !isOwner, Privacy = privacy };

        switch (privacy.Visibility)
        {
            case ProfileVisibilityOptions.Public:
                return new ProfileAccess { CanView = true, Privacy = privacy };
            case ProfileVisibilityOptions.RegisteredUsers:
                return viewerId != null
                    ? new ProfileAccess { CanView = true, Privacy = privacy }
                    : new ProfileAccess { Reason = ProfileDenialReason.SignInRequired, Privacy = privacy };
            default:
                if (viewerId != null && await HasRelationshipAsync(viewerId, targetUserId))
                    return new ProfileAccess { CanView = true, ViaRelationship = true, Privacy = privacy };
                return new ProfileAccess { Reason = viewerId == null ? ProfileDenialReason.SignInRequired : ProfileDenialReason.Private, Privacy = privacy };
        }
    }

    public async Task<Dictionary<string, UserPrivacy>> GetPrivacyAsync(IEnumerable<string> userIds)
    {
        var ids = userIds.Where(id => !string.IsNullOrEmpty(id)).Distinct().ToList();
        var rows = await _db.UserSettings.AsNoTracking().Where(s => ids.Contains(s.UserId)).ToListAsync();
        var map = rows.ToDictionary(s => s.UserId, UserPrivacy.From);
        foreach (var id in ids) map.TryAdd(id, UserPrivacy.Default);
        return map;
    }

    public async Task<HashSet<string>> GetViewableAsync(IEnumerable<string> userIds, string? viewerId, bool viewerIsAdmin)
    {
        var privacy = await GetPrivacyAsync(userIds);
        var result = new HashSet<string>();
        var privateIds = new List<string>();
        foreach (var (id, p) in privacy)
        {
            if (viewerIsAdmin || id == viewerId || p.Visibility == ProfileVisibilityOptions.Public
                || (p.Visibility == ProfileVisibilityOptions.RegisteredUsers && viewerId != null))
                result.Add(id);
            else if (p.IsPrivate && viewerId != null)
                privateIds.Add(id);
        }
        if (privateIds.Count > 0)
        {
            foreach (var id in await RelatedUserIdsAsync(viewerId!, privateIds)) result.Add(id);
        }
        return result;
    }

    private async Task<bool> HasRelationshipAsync(string viewerId, string targetId) =>
        (await RelatedUserIdsAsync(viewerId, new List<string> { targetId })).Count > 0;

    /// <summary>Of <paramref name="candidates"/>, users connected to (accepted) or on a team with the viewer.</summary>
    private async Task<List<string>> RelatedUserIdsAsync(string viewerId, List<string> candidates)
    {
        var connected = await _db.Connections.AsNoTracking()
            .Where(c => c.Status == ConnectionStatus.Accepted
                && ((c.RequesterId == viewerId && candidates.Contains(c.AddresseeId))
                    || (c.AddresseeId == viewerId && candidates.Contains(c.RequesterId))))
            .Select(c => c.RequesterId == viewerId ? c.AddresseeId : c.RequesterId)
            .ToListAsync();

        var viewerTeams = _db.TeamMembers.Where(m => m.UserId == viewerId).Select(m => m.TeamId);
        var teammates = await _db.TeamMembers.AsNoTracking()
            .Where(m => candidates.Contains(m.UserId) && viewerTeams.Contains(m.TeamId))
            .Select(m => m.UserId)
            .ToListAsync();

        return connected.Concat(teammates).Distinct().ToList();
    }
}
