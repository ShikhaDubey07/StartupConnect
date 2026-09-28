using System.ComponentModel.DataAnnotations;
using StartupConnect.Models;
using StartupConnect.Services;

namespace StartupConnect.ViewModels;

// ---------------- Challenges ----------------

public class ChallengeCardViewModel
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Prize { get; set; } = string.Empty;
    public DateTime DeadlineUtc { get; set; }
    public bool IsActive { get; set; }
    public bool IsOpen { get; set; }
    public bool ResultsAnnounced { get; set; }
    public string? CategoryName { get; set; }
    public string? CategoryIcon { get; set; }
    public string? CoverImageUrl { get; set; }
    public int SubmissionCount { get; set; }
    public List<string> WinnerIdeaTitles { get; set; } = new();
    public bool HasMySubmission { get; set; }
}

public class ChallengeIndexViewModel
{
    public string Tab { get; set; } = "active";
    public List<ChallengeCardViewModel> Active { get; set; } = new();
    public List<ChallengeCardViewModel> Past { get; set; } = new();
}

public class ChallengeSubmissionRowViewModel
{
    public int Id { get; set; }
    public int IdeaId { get; set; }
    public string IdeaTitle { get; set; } = string.Empty;
    public string IdeaTagline { get; set; } = string.Empty;
    public bool IdeaIsPublic { get; set; }
    public string FounderId { get; set; } = string.Empty;
    public string FounderName { get; set; } = string.Empty;
    public bool FounderVerified { get; set; }
    public DateTime SubmittedAt { get; set; }
    public string PitchNotes { get; set; } = string.Empty;
    public bool IsMine { get; set; }
    public bool IsWinner { get; set; }
    public bool IsShortlisted { get; set; }
    public string? AwardTitle { get; set; }
    public string? JudgeFeedback { get; set; }
}

public class ChallengeDetailsViewModel
{
    public StartupChallenge Challenge { get; set; } = null!;
    public bool IsOpen { get; set; }
    public bool IsAuthenticated { get; set; }
    public bool EmailConfirmed { get; set; }
    public bool IsAdmin { get; set; }
    /// <summary>The viewer's approved ideas that are not yet entered.</summary>
    public List<(int Id, string Title)> EligibleIdeas { get; set; } = new();
    public int MyApprovedIdeaCount { get; set; }
    public int MyPendingIdeaCount { get; set; }
    public List<ChallengeSubmissionRowViewModel> Submissions { get; set; } = new();
    public List<ChallengeSubmissionRowViewModel> MySubmissions { get; set; } = new();
    public List<ChallengeSubmissionRowViewModel> Winners { get; set; } = new();
    public List<ChallengeSubmissionRowViewModel> Shortlisted { get; set; } = new();
}

public class ChallengeFormViewModel
{
    public int? Id { get; set; }

    [Required, StringLength(150)]
    public string Title { get; set; } = string.Empty;

    [Required, StringLength(4000)]
    public string Description { get; set; } = string.Empty;

    [Required, StringLength(150)]
    [Display(Name = "Prize")]
    public string Prize { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Deadline (IST)")]
    [DataType(DataType.DateTime)]
    public DateTime? DeadlineIst { get; set; }

    [StringLength(2000)]
    [Display(Name = "Eligibility")]
    public string? Eligibility { get; set; }

    [StringLength(4000)]
    [Display(Name = "Rules & judging criteria")]
    public string? Rules { get; set; }

    [Url, StringLength(500)]
    [Display(Name = "Cover image URL")]
    public string? CoverImageUrl { get; set; }

    [Display(Name = "Category")]
    public int? CategoryId { get; set; }

    [Display(Name = "Open for submissions")]
    public bool IsActive { get; set; } = true;

    [Microsoft.AspNetCore.Mvc.ModelBinding.BindNever]
    public int SubmissionCount { get; set; }
}

public class AdminChallengeRowViewModel
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Prize { get; set; } = string.Empty;
    public DateTime DeadlineUtc { get; set; }
    public bool IsActive { get; set; }
    public bool IsOpen { get; set; }
    public bool ResultsAnnounced { get; set; }
    public int SubmissionCount { get; set; }
    public int WinnerCount { get; set; }
    public int ShortlistCount { get; set; }
    public string? CategoryName { get; set; }
}

public class AdminJudgeViewModel
{
    public StartupChallenge Challenge { get; set; } = null!;
    public bool IsOpen { get; set; }
    public List<ChallengeSubmissionRowViewModel> Submissions { get; set; } = new();
}

// ---------------- Workspace ----------------

public class WorkspaceMemberViewModel
{
    public int MemberId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public bool IsFounder { get; set; }
    public bool IsVerified { get; set; }
    public string? PhotoUrl { get; set; }
    public DateTime JoinedAt { get; set; }
}

public class MilestoneViewModel
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime? DueDate { get; set; }
    public bool IsCompleted { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? AssigneeUserId { get; set; }
    public string? AssigneeName { get; set; }
    public bool IsOverdue => !IsCompleted && DueDate.HasValue && DueDate.Value.Date < Infrastructure.AppTime.ToIst(DateTime.UtcNow).Date;
}

public class RoadmapViewModel
{
    public List<MilestoneViewModel> Milestones { get; set; } = new();
    public int Done => Milestones.Count(m => m.IsCompleted);
    public int Total => Milestones.Count;
    public int Percent => Total == 0 ? 0 : (int)Math.Round(100.0 * Done / Total);
}

public class WorkspaceViewModel
{
    public int TeamId { get; set; }
    public string TeamName { get; set; } = string.Empty;
    public TeamStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public int IdeaId { get; set; }
    public string IdeaTitle { get; set; } = string.Empty;
    public string IdeaTagline { get; set; } = string.Empty;
    public string IdeaCategory { get; set; } = string.Empty;
    public IdeaStatus IdeaStatus { get; set; }
    public IdeaProgressStage ProgressStage { get; set; }
    public decimal MinimumFundRequired { get; set; }
    public int ExpectedTeamSize { get; set; }
    public List<string> RolesNeeded { get; set; } = new();
    public string CurrentUserId { get; set; } = string.Empty;
    public bool IsFounder { get; set; }
    public List<WorkspaceMemberViewModel> Members { get; set; } = new();
    public RoadmapViewModel Roadmap { get; set; } = new();
    public List<TeamMessageDto> Messages { get; set; } = new();
    public bool HasOlderMessages { get; set; }
    public bool IsClosed => Status == TeamStatus.Closed;
}

// ---------------- Founder analytics ----------------

public class FounderIdeaStat
{
    public int IdeaId { get; set; }
    public string Title { get; set; } = string.Empty;
    public IdeaStatus Status { get; set; }
    public int Views { get; set; }
    public int Likes { get; set; }
    public int Comments { get; set; }
    public int Interests { get; set; }
    public int Score => Views + Likes * 5 + Comments * 10;
}

public class FounderAnalyticsViewModel
{
    public int TotalViews { get; set; }
    public int TotalLikes { get; set; }
    public int TotalComments { get; set; }
    public int TotalInterests { get; set; }
    public int IdeaCount { get; set; }
    public double AverageEngagement { get; set; }
    public List<FounderIdeaStat> Ideas { get; set; } = new();
    public List<UserActivity> Activities { get; set; } = new();
    public int MilestonesCompleted { get; set; }
    public int ChallengeEntries { get; set; }
}

// ---------------- Admin moderation ----------------

public class AdminReportsViewModel
{
    public bool ShowHistory { get; set; }
    public List<ReportedIdeaGroup> Groups { get; set; } = new();
}

public class AdminVerificationViewModel
{
    public List<FounderVerificationRequest> Pending { get; set; } = new();
    public List<FounderVerificationRequest> RecentDecisions { get; set; } = new();
    /// <summary>Approved ideas per pending applicant (context for the decision).</summary>
    public Dictionary<string, int> ApprovedIdeaCounts { get; set; } = new();
}

// ---------------- Shared ----------------

/// <summary>Model for the _Avatar partial: photo when a safe URL exists, otherwise coloured initials.</summary>
public sealed record AvatarModel(string? Name, string? PhotoUrl = null, int Size = 40)
{
    public string Initials
    {
        get
        {
            var parts = (Name ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return "?";
            var first = char.ToUpperInvariant(parts[0][0]);
            return parts.Length > 1 ? $"{first}{char.ToUpperInvariant(parts[^1][0])}" : first.ToString();
        }
    }

    /// <summary>Stable colour index (0–5) derived from the name.</summary>
    public int ColorIndex => (int)((uint)(Name ?? string.Empty).Aggregate(17, (h, c) => unchecked(h * 31 + c)) % 6);
}
