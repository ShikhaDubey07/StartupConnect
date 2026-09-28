namespace StartupConnect.Models;

public class StartupChallenge
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Prize { get; set; } = string.Empty;
    /// <summary>Submission deadline, stored in UTC (entered/displayed in IST — see <c>AppTime</c>).</summary>
    public DateTime Deadline { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    /// <summary>False once an admin closes the challenge (reopening sets it back to true).</summary>
    public bool IsActive { get; set; } = true;

    public string? Eligibility { get; set; }
    public string? Rules { get; set; }
    /// <summary>Optional http(s) cover image. When empty a gradient cover is shown.</summary>
    public string? CoverImageUrl { get; set; }

    public int? CategoryId { get; set; }
    public Category? Category { get; set; }

    /// <summary>Set when an admin announces the results; winners become public from then on.</summary>
    public DateTime? ResultsAnnouncedAt { get; set; }

    public ICollection<ChallengeSubmission> Submissions { get; set; } = new List<ChallengeSubmission>();

    /// <summary>Accepting submissions right now.</summary>
    public bool IsOpenForSubmissions(DateTime utcNow) => IsActive && Deadline > utcNow;

    public bool ResultsAnnounced => ResultsAnnouncedAt.HasValue;
}
