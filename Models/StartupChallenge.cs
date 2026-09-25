namespace StartupConnect.Models;

public class StartupChallenge
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Prize { get; set; } = string.Empty;
    public DateTime Deadline { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;

    public ICollection<ChallengeSubmission> Submissions { get; set; } = new List<ChallengeSubmission>();
}
