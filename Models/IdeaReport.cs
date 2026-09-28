namespace StartupConnect.Models;

public enum ReportStatus
{
    Open,
    Dismissed,
    Resolved
}

public class IdeaReport
{
    public int Id { get; set; }
    public int IdeaId { get; set; }
    public Idea Idea { get; set; } = null!;

    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;

    public string Reason { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // ---- Moderation ----
    public ReportStatus Status { get; set; } = ReportStatus.Open;
    public string? ResolvedByUserId { get; set; }
    public ApplicationUser? ResolvedBy { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public string? ResolutionNote { get; set; }
}
