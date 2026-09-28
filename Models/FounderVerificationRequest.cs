namespace StartupConnect.Models;

public enum VerificationRequestStatus
{
    Pending,
    Approved,
    Rejected
}

/// <summary>
/// A user's request for the "Verified Founder" badge. The latest request drives the UI; older ones
/// are kept as history. <see cref="UserProfile.IsVerifiedFounder"/> is the flag the badge reads.
/// </summary>
public class FounderVerificationRequest
{
    public int Id { get; set; }

    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;

    public string Note { get; set; } = string.Empty;
    public string? LinkedInUrl { get; set; }
    public VerificationRequestStatus Status { get; set; } = VerificationRequestStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public string? ReviewedByUserId { get; set; }
    public ApplicationUser? ReviewedBy { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? RejectionReason { get; set; }
}
