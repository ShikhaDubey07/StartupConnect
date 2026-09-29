namespace StartupConnect.Models;

/// <summary>
/// One counted view of an idea. Views are de-duplicated per (idea, viewer) per 24 hours by
/// <see cref="StartupConnect.Services.IdeaViewTracker"/>, so each row is a "unique daily view".
/// </summary>
public class IdeaView
{
    public const int ViewerKeyMaxLength = 80;

    public int Id { get; set; }
    public int IdeaId { get; set; }
    public Idea Idea { get; set; } = null!;

    public string? UserId { get; set; }
    public ApplicationUser? User { get; set; }

    /// <summary>
    /// Stable, non-reversible viewer identity used for de-duplication: "u:{userId}" for members,
    /// "a:{sha256 of the visitor cookie}" for anonymous visitors. No IP addresses are stored.
    /// </summary>
    public string? ViewerKey { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
