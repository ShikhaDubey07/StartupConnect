namespace StartupConnect.Models;

public class ChallengeSubmission
{
    public int Id { get; set; }

    public int ChallengeId { get; set; }
    public StartupChallenge Challenge { get; set; } = null!;

    public int IdeaId { get; set; }
    public Idea Idea { get; set; } = null!;

    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
    public string PitchNotes { get; set; } = string.Empty;

    // ---- Judging (admin/panel) ----
    public bool IsShortlisted { get; set; }
    public bool IsWinner { get; set; }
    /// <summary>Optional award label for winners, e.g. "Winner", "Runner-up", "Jury's Choice".</summary>
    public string? AwardTitle { get; set; }
    /// <summary>Feedback from the judges, shared with the submitter when results are announced.</summary>
    public string? JudgeFeedback { get; set; }
}
