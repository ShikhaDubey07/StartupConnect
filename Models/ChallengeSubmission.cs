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
}
