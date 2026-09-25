namespace StartupConnect.Models;

/// <summary>
/// Caches the AI-generated one-sentence rationale explaining why two users are a good co-founder match.
/// Cache key is (UserId1, UserId2) — always stored with the lower userId first for consistency.
/// </summary>
public class CoFounderMatchInsight
{
    public int Id { get; set; }

    /// <summary>The alphabetically-first user ID of the pair.</summary>
    public string UserId1 { get; set; } = string.Empty;

    /// <summary>The alphabetically-second user ID of the pair.</summary>
    public string UserId2 { get; set; } = string.Empty;

    /// <summary>AI-generated one-sentence rationale explaining the match.</summary>
    public string Rationale { get; set; } = string.Empty;

    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}
