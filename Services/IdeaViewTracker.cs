using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using StartupConnect.Data;
using StartupConnect.Models;

namespace StartupConnect.Services;

public interface IIdeaViewTracker
{
    /// <summary>
    /// Records a view of <paramref name="ideaId"/> unless it's the owner, a bot, or the same viewer already
    /// counted within the last 24 hours. Returns true when a view row was written.
    /// </summary>
    Task<bool> TrackAsync(HttpContext http, int ideaId, string ownerUserId);
}

/// <summary>
/// De-duplicated idea view counting. Members are keyed by user id; anonymous visitors by a random id kept in an
/// HttpOnly cookie (<see cref="VisitorCookie"/>) which is stored only as a salted SHA-256 hash. IP addresses are never stored.
/// </summary>
public sealed partial class IdeaViewTracker : IIdeaViewTracker
{
    public const string VisitorCookie = "sc_vid";
    public static readonly TimeSpan DedupeWindow = TimeSpan.FromHours(24);

    private readonly ApplicationDbContext _db;
    private readonly string _salt;

    public IdeaViewTracker(ApplicationDbContext db, IConfiguration config)
    {
        _db = db;
        _salt = config["Analytics:VisitorSalt"] ?? "startupconnect-visitor-v1";
    }

    [GeneratedRegex(@"bot|crawl|spider|slurp|scrap|facebookexternalhit|embedly|preview|headless|phantomjs|lighthouse|pingdom|monitor|curl|wget|python-requests|httpclient|okhttp|java/|go-http|axios|node-fetch",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex BotPattern();

    public static bool IsBot(string? userAgent) => string.IsNullOrWhiteSpace(userAgent) || BotPattern().IsMatch(userAgent);

    public async Task<bool> TrackAsync(HttpContext http, int ideaId, string ownerUserId)
    {
        if (!HttpMethods.IsGet(http.Request.Method)) return false;
        if (IsBot(http.Request.Headers.UserAgent.ToString())) return false;

        var userId = http.User.Identity?.IsAuthenticated == true
            ? http.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            : null;
        if (userId != null && userId == ownerUserId) return false;

        string viewerKey;
        if (userId != null)
        {
            viewerKey = "u:" + userId;
        }
        else
        {
            var visitorId = http.Request.Cookies[VisitorCookie];
            if (string.IsNullOrEmpty(visitorId) || visitorId.Length > 64)
            {
                visitorId = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
                http.Response.Cookies.Append(VisitorCookie, visitorId, new CookieOptions
                {
                    HttpOnly = true,
                    IsEssential = true,
                    SameSite = SameSiteMode.Lax,
                    Secure = http.Request.IsHttps,
                    MaxAge = TimeSpan.FromDays(365)
                });
            }
            viewerKey = "a:" + Hash(visitorId);
        }

        var since = DateTime.UtcNow - DedupeWindow;
        if (await _db.IdeaViews.AnyAsync(v => v.IdeaId == ideaId && v.ViewerKey == viewerKey && v.CreatedAt >= since))
            return false;

        _db.IdeaViews.Add(new IdeaView { IdeaId = ideaId, UserId = userId, ViewerKey = viewerKey, CreatedAt = DateTime.UtcNow });
        await _db.SaveChangesAsync();
        return true;
    }

    private string Hash(string visitorId)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(_salt + ":" + visitorId));
        return Convert.ToHexString(bytes, 0, 24).ToLowerInvariant(); // 48 hex chars
    }
}
