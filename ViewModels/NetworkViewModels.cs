using StartupConnect.Models;

namespace StartupConnect.ViewModels;

/// <summary>The viewer's relationship with another member, from the viewer's side.</summary>
public enum ConnectionState
{
    None,
    Self,
    OutgoingPending,
    IncomingPending,
    Connected
}

/// <summary>Model for Views/Shared/_ConnectButton.cshtml. Variant: "card" (compact) or "profile" (full width).</summary>
public sealed record ConnectButtonModel(string TargetUserId, ConnectionState State, string? TargetName = null, string Variant = "card");

public class NetworkPersonViewModel
{
    public int ConnectionId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? PhotoUrl { get; set; }
    public bool IsInvestor { get; set; }
    public bool IsVerified { get; set; }
    /// <summary>"City, State" when the member shares their location; otherwise null.</summary>
    public string? Location { get; set; }
    public List<string> Skills { get; set; } = new();
    public string? Message { get; set; }
    public DateTime Date { get; set; }
    public bool CanViewProfile { get; set; }
}

public class NetworkViewModel
{
    public string Tab { get; set; } = "connections";
    public List<NetworkPersonViewModel> Connections { get; set; } = new();
    public List<NetworkPersonViewModel> Incoming { get; set; } = new();
    public List<NetworkPersonViewModel> Sent { get; set; } = new();
}

public class PrivateProfileViewModel
{
    public string UserId { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? PhotoUrl { get; set; }
    public bool SignInRequired { get; set; }
    public ConnectionState ConnectionState { get; set; }
    /// <summary>The owner is previewing how a visitor sees their restricted profile.</summary>
    public bool IsOwnerPreview { get; set; }
}

public class ProfilePrivacyHint
{
    public string Visibility { get; set; } = ProfileVisibilityOptions.Public;
    public bool ShowEmail { get; set; }
    public bool ShowLocation { get; set; }
    public bool ShowAge { get; set; }
    public bool IsPreview { get; set; }
}

public class InvestorDashboardFilter
{
    public int? CategoryId { get; set; }
    public IdeaProgressStage? Stage { get; set; }
    [System.ComponentModel.DataAnnotations.Range(0, 1_000_000_000)]
    public decimal? MinFund { get; set; }
    [System.ComponentModel.DataAnnotations.Range(0, 1_000_000_000)]
    public decimal? MaxFund { get; set; }
    /// <summary>Only ideas in the investor's interest categories.</summary>
    public bool MyCategories { get; set; }

    public bool IsActive => CategoryId.HasValue || Stage.HasValue || MinFund.HasValue || MaxFund.HasValue || MyCategories;
}

public class InvestorIdeaMatch
{
    public int IdeaId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Tagline { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public string CategoryIcon { get; set; } = "bi-lightbulb";
    public IdeaProgressStage Stage { get; set; }
    public decimal FundingAsk { get; set; }
    public decimal Committed { get; set; }
    public decimal Remaining => Math.Max(0, FundingAsk - Committed);
    public int Score { get; set; }
    public List<string> Reasons { get; set; } = new();
    public string FounderName { get; set; } = string.Empty;
    public bool FounderVerified { get; set; }
    public DateTime? PublishedAt { get; set; }
    public int Likes { get; set; }
    public int Comments { get; set; }
    public int Views { get; set; }
    public bool IsSaved { get; set; }
    public InterestStatus? MyInterestStatus { get; set; }
}

public class InvestorInterestRow
{
    public int InterestId { get; set; }
    public int IdeaId { get; set; }
    public string IdeaTitle { get; set; } = string.Empty;
    public bool IdeaIsLive { get; set; }
    public InterestType InterestType { get; set; }
    public decimal? Amount { get; set; }
    public InterestStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class InvestorSavedIdea
{
    public int IdeaId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public decimal FundingAsk { get; set; }
    public DateTime SavedAt { get; set; }
}

public class InvestorDashboardViewModel
{
    public bool IsInvestor { get; set; }
    public InvestmentCapacity Capacity { get; set; }
    public List<Category> InterestCategories { get; set; } = new();
    public bool NeedsProfileSetup => !IsInvestor || Capacity == InvestmentCapacity.None || InterestCategories.Count == 0;

    public InvestorDashboardFilter Filter { get; set; } = new();
    public List<Category> AllCategories { get; set; } = new();
    public List<InvestorIdeaMatch> Matches { get; set; } = new();
    public int CandidateCount { get; set; }

    public List<InvestorInterestRow> Interests { get; set; } = new();
    public decimal TotalProposed { get; set; }
    public decimal AcceptedTotal { get; set; }
    public int PendingInterests { get; set; }
    public List<InvestorSavedIdea> SavedIdeas { get; set; } = new();
}
