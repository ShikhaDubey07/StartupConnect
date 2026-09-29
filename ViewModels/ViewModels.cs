using System.ComponentModel.DataAnnotations;
using StartupConnect.Models;

namespace StartupConnect.ViewModels;

public class RegisterViewModel
{
    [Required, Display(Name = "Full Name")]
    [StringLength(100)]
    public string FullName { get; set; } = string.Empty;

    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required, Phone]
    [Display(Name = "Phone Number")]
    public string PhoneNumber { get; set; } = string.Empty;

    [Required, StringLength(100, MinimumLength = 8, ErrorMessage = "Password must be at least 8 characters.")]
    [RegularExpression(PasswordRules.Pattern, ErrorMessage = PasswordRules.Message)]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Required, Compare("Password", ErrorMessage = "Passwords do not match.")]
    [DataType(DataType.Password)]
    [Display(Name = "Confirm Password")]
    public string ConfirmPassword { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Participation Type")]
    public string Role { get; set; } = string.Empty;

    public string? Location { get; set; }

    [Display(Name = "Time Availability")]
    public TimeAvailability TimeAvailability { get; set; }

    [Display(Name = "Investment Capacity")]
    public InvestmentCapacity InvestmentCapacity { get; set; }

    [Display(Name = "Interests")]
    public List<int> SelectedCategoryIds { get; set; } = new();

    [Display(Name = "Skills")]
    public List<string> SelectedSkills { get; set; } = new();
}

/// <summary>Client + server hints matching the Identity password policy configured in Program.cs.</summary>
public static class PasswordRules
{
    public const string Pattern = @"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^a-zA-Z\d]).{8,}$";
    public const string Message = "Use at least 8 characters with an uppercase letter, a lowercase letter, a number and a symbol.";
}

public class ConfirmEmailResultViewModel
{
    public bool Succeeded { get; set; }
    public bool AlreadyConfirmed { get; set; }
    public string? Email { get; set; }
}

public class ResendConfirmationViewModel
{
    [Required, EmailAddress, StringLength(256)]
    public string Email { get; set; } = string.Empty;

    public bool Sent { get; set; }
}

public class DeleteAccountViewModel
{
    [Required(ErrorMessage = "Please enter your password to confirm.")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Type DELETE to confirm.")]
    public string Confirmation { get; set; } = string.Empty;
}

public class ForgotPasswordViewModel
{
    [Required, EmailAddress, StringLength(256)]
    public string Email { get; set; } = string.Empty;
}

public class LoginViewModel
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required, DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Display(Name = "Remember me")]
    public bool RememberMe { get; set; }
}

public class ProfileViewModel
{
    [StringLength(100)]
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    [StringLength(100)]
    public string? City { get; set; }
    [StringLength(100)]
    public string? State { get; set; }
    [Range(13, 120)]
    public int? Age { get; set; }

    [StringLength(500)]
    public string? Bio { get; set; }

    [Display(Name = "Time Availability")]
    public TimeAvailability TimeAvailability { get; set; }

    [Range(1, 80)]
    [Display(Name = "Hours Per Week")]
    public int HoursPerWeek { get; set; }

    [Display(Name = "Investment Capacity")]
    public InvestmentCapacity InvestmentCapacity { get; set; }

    [Url, StringLength(300), Display(Name = "LinkedIn URL")]
    public string? LinkedInUrl { get; set; }
    [Url, StringLength(300), Display(Name = "Portfolio URL")]
    public string? PortfolioUrl { get; set; }
    public bool IsInvestor { get; set; }

    public List<int> SelectedCategoryIds { get; set; } = new();
    public List<string> SelectedSkills { get; set; } = new();

    /// <summary>Display-only; never bound from requests.</summary>
    [Microsoft.AspNetCore.Mvc.ModelBinding.BindNever]
    public int ProfileCompletionPercent { get; set; }

    public static readonly string[] AvailableSkills =
        ["Developer", "Designer", "Marketing", "Sales", "Operations", "Finance", "Content Creator", "Medical Advisor"];
}

public class IdeaSubmitViewModel
{
    public int? Id { get; set; }

    [Required, StringLength(150)]
    public string Title { get; set; } = string.Empty;

    [Required, StringLength(200)]
    public string Tagline { get; set; } = string.Empty;

    [Required, StringLength(3000)]
    public string Description { get; set; } = string.Empty;

    [Required, StringLength(1000)]
    [Display(Name = "Problem Statement")]
    public string ProblemStatement { get; set; } = string.Empty;

    [Required, StringLength(1000)]
    public string Solution { get; set; } = string.Empty;

    [Required, StringLength(500)]
    [Display(Name = "Target Market")]
    public string TargetMarket { get; set; } = string.Empty;

    [Required, StringLength(500)]
    [Display(Name = "Business Model")]
    public string BusinessModel { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Category")]
    public int? CategoryId { get; set; }

    [StringLength(100)]
    [Display(Name = "Custom Category")]
    public string? CustomCategory { get; set; }

    [Required, Range(1000, 100000000)]
    [Display(Name = "Minimum Fund Required (₹)")]
    public decimal MinimumFundRequired { get; set; }

    [Range(2, 6000)]
    [Display(Name = "Expected Team Size")]
    public int ExpectedTeamSize { get; set; } = 3;

    [Required, MinLength(1)]
    [Display(Name = "Roles Needed")]
    public List<string> RolesNeeded { get; set; } = new();

    public bool AcceptTerms { get; set; }
}

public class IdeaBrowseViewModel
{
    public const int MaxPageSize = 48;

    public string? Search { get; set; }
    public int? CategoryId { get; set; }
    public IdeaProgressStage? Stage { get; set; }
    public decimal? MinFund { get; set; }
    public decimal? MaxFund { get; set; }
    /// <summary>Ideas needing any of these roles (values from <see cref="ProfileViewModel.AvailableSkills"/>).</summary>
    public List<string> Roles { get; set; } = new();
    public bool VerifiedOnly { get; set; }
    /// <summary>newest | liked | viewed | trending | fund-asc | fund-desc | interest | ai-score</summary>
    public string SortBy { get; set; } = "newest";
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 9;

    public static readonly (string Value, string Label)[] SortOptions =
    [
        ("newest", "Newest"),
        ("trending", "Trending"),
        ("liked", "Most liked"),
        ("viewed", "Most viewed"),
        ("interest", "Most interest"),
        ("fund-asc", "Funding: low to high"),
        ("fund-desc", "Funding: high to low"),
        ("ai-score", "AI score")
    ];

    public bool HasFilters => !string.IsNullOrWhiteSpace(Search) || CategoryId.HasValue || Stage.HasValue || MinFund.HasValue
        || MaxFund.HasValue || Roles.Count > 0 || VerifiedOnly;

    /// <summary>Route values for links (pagination) that keep the current filter state.</summary>
    public Dictionary<string, string> ToRouteValues(int? page = null)
    {
        var d = new Dictionary<string, string>();
        if (!string.IsNullOrWhiteSpace(Search)) d["Search"] = Search.Trim();
        if (CategoryId.HasValue) d["CategoryId"] = CategoryId.Value.ToString();
        if (Stage.HasValue) d["Stage"] = Stage.Value.ToString();
        if (MinFund.HasValue) d["MinFund"] = MinFund.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (MaxFund.HasValue) d["MaxFund"] = MaxFund.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        for (var i = 0; i < Roles.Count; i++) d[$"Roles[{i}]"] = Roles[i];
        if (VerifiedOnly) d["VerifiedOnly"] = "true";
        if (!string.IsNullOrWhiteSpace(SortBy) && SortBy != "newest") d["SortBy"] = SortBy;
        if (PageSize != 9) d["PageSize"] = PageSize.ToString();
        if (page.HasValue && page.Value > 1) d["Page"] = page.Value.ToString();
        return d;
    }
}

public class IdeaCardViewModel
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Tagline { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public string CategoryIcon { get; set; } = "bi-lightbulb";
    public decimal MinimumFundRequired { get; set; }
    public int ExpectedTeamSize { get; set; }
    public int InterestCount { get; set; }
    public int LikesCount { get; set; }
    public bool IsSavedByCurrentUser { get; set; }
    public List<string> RolesNeeded { get; set; } = new();
    public DateTime? PublishedAt { get; set; }
    /// <summary>AI OverallScore (0–100) from IdeaAnalysis. Null if not yet analyzed.</summary>
    public int? AiScore { get; set; }
    public bool SubmitterVerified { get; set; }
    public int ViewsCount { get; set; }
    public IdeaProgressStage ProgressStage { get; set; }
}


public class IdeaDetailViewModel
{
    public int Id { get; set; }
    public IdeaStatus Status { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Tagline { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public IdeaHistoryViewModel? PreviousVersion { get; set; }
    public string ProblemStatement { get; set; } = string.Empty;
    public string Solution { get; set; } = string.Empty;
    public string TargetMarket { get; set; } = string.Empty;
    public string BusinessModel { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public decimal MinimumFundRequired { get; set; }
    public int ExpectedTeamSize { get; set; }
    public List<string> RolesNeeded { get; set; } = new();
    public string SubmitterName { get; set; } = string.Empty;
    public string SubmitterId { get; set; } = string.Empty;
    public bool SubmitterVerified { get; set; }
    public string? SubmitterCity { get; set; }
    public int InterestCount { get; set; }
    public decimal TotalPledged { get; set; }
    public bool CanShowInterest { get; set; }
    public bool IsOwner { get; set; }
    public bool IsLikedByCurrentUser { get; set; }
    public bool IsSavedByCurrentUser { get; set; }
    public int LikesCount { get; set; }
    public DateTime? PublishedAt { get; set; }
    public List<IdeaCommentViewModel> Comments { get; set; } = new();
    public IdeaAnalysisViewModel? Analysis { get; set; }
}

public class IdeaCommentViewModel
{
    public int Id { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class IdeaHistoryViewModel
{
    public int Id { get; set; }
    public int VersionNumber { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Solution { get; set; } = string.Empty;
    public string ProblemStatement { get; set; } = string.Empty;
    public string TargetMarket { get; set; } = string.Empty;
    public string BusinessModel { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public decimal MinimumFundRequired { get; set; }
    public int ExpectedTeamSize { get; set; }
    public DateTime EditedAt { get; set; }
    public string EditorName { get; set; } = string.Empty;
    public string ChangeSummary { get; set; } = string.Empty;
}

public class ShowInterestViewModel
{
    public int IdeaId { get; set; }

    [Required, EnumDataType(typeof(InterestType))]
    public InterestType InterestType { get; set; }

    [Range(0, 100000000)]
    [Display(Name = "Investment Amount (₹)")]
    public decimal? ProposedInvestmentAmount { get; set; }

    public List<string> SelectedRoles { get; set; } = new();

    [StringLength(500)]
    public string? Message { get; set; }
}

public class DashboardViewModel
{
    public string UserName { get; set; } = string.Empty;
    public int ProfileCompletion { get; set; }
    public int MyIdeasCount { get; set; }
    public int MyInterestsCount { get; set; }
    public int UnreadNotifications { get; set; }
    public List<IdeaCardViewModel> FeaturedIdeas { get; set; } = new();
    public List<IdeaCardViewModel> MyIdeas { get; set; } = new();
}

public class AdminDashboardViewModel
{
    public int TotalUsers { get; set; }
    public int TotalIdeas { get; set; }
    public int PendingIdeas { get; set; }
    public int ApprovedIdeas { get; set; }
    public int TotalInterests { get; set; }
    public List<Idea> PendingIdeasList { get; set; } = new();
    public int PendingVerifications { get; set; }
    public int OpenReports { get; set; }
    public int ReportedIdeas { get; set; }
    public int ActiveChallenges { get; set; }
    public int ChallengesAwaitingResults { get; set; }
}

public class ContactViewModel
{
    [Required, StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required, StringLength(200)]
    public string Subject { get; set; } = string.Empty;

    [Required, StringLength(2000)]
    public string Message { get; set; } = string.Empty;
}

public class IdeaMatchViewModel
{
    public int IdeaId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public string TargetMarket { get; set; } = string.Empty;
    public List<StartupConnect.Services.Matching.SimilarIdeaMatch> Matches { get; set; } = new();
}

public class AdminUserViewModel
{
    public string Id { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public int IdeasSubmittedCount { get; set; }
}

public class AdminIdeaViewModel
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string SubmitterName { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public IdeaStatus Status { get; set; }
    public decimal MinimumFundRequired { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class HelpIndexViewModel
{
    public List<SupportTicket> MyTickets { get; set; } = new();
}

public class CreateTicketViewModel
{
    [Required, StringLength(50)]
    [Display(Name = "Issue Category")]
    public string IssueCategory { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string Subject { get; set; } = string.Empty;

    [Required]
    [StringLength(2000)]
    public string Description { get; set; } = string.Empty;
}

public class TicketDetailViewModel
{
    public SupportTicket Ticket { get; set; } = null!;
    public List<SupportTicketMessage> Messages { get; set; } = new();
    
    [Required, StringLength(2000)]
    public string NewMessage { get; set; } = string.Empty;
}

public class IdeaAnalyticsViewModel
{
    public Idea Idea { get; set; } = null!;
    
    /// <summary>Unique daily views (one per viewer per 24h).</summary>
    public int TotalViews { get; set; }
    /// <summary>Distinct viewers (members + anonymous visitors) ever.</summary>
    public int UniqueViewers { get; set; }
    public int TotalLikes { get; set; }
    public int TotalSaves { get; set; }
    public int TotalComments { get; set; }
    public int TotalInterests { get; set; }
    public int TotalMatches { get; set; }
    
    public List<string> Dates { get; set; } = new();
    public List<int> ViewsData { get; set; } = new();
    public List<int> LikesData { get; set; } = new();
    public List<int> SavesData { get; set; } = new();
    public List<int> CommentsData { get; set; } = new();
}

public class AnalyticsDetailItemViewModel
{
    public string? UserName { get; set; }
    public string? UserProfilePhoto { get; set; }
    public string ActivityType { get; set; } = string.Empty;
    public DateTime ActivityDate { get; set; }
    public string? ExtraDetails { get; set; }
}

public class FindTeamViewModel
{
    public string? Role { get; set; } // "", "Founder", "Investor"
    public string? Skill { get; set; }
    public int? IndustryId { get; set; }
    public TimeAvailability? Availability { get; set; }
    /// <summary>"For idea" mode: rank people by the roles this idea (owned by the viewer) still needs.</summary>
    public int? ForIdeaId { get; set; }
    public bool IncludeConnections { get; set; }

    [Microsoft.AspNetCore.Mvc.ModelBinding.BindNever]
    public StartupConnect.Services.Matching.TeamMatchResults Results { get; set; } = new();
}

public class IdeaAnalysisViewModel
{
    public string Summary { get; set; } = string.Empty;
    public string ProblemsAndSolutions { get; set; } = string.Empty;
    public string TargetUsers { get; set; } = string.Empty;
    public string MarketPotential { get; set; } = string.Empty;
    public string Risks { get; set; } = string.Empty;
    public string RevenueModels { get; set; } = string.Empty;
    public string TeamSuggestions { get; set; } = string.Empty;
    public int OverallScore { get; set; }
    public List<SimilarIdeaViewModel> SimilarIdeas { get; set; } = new();
    public DateTime GeneratedAt { get; set; }
}

public class SimilarIdeaViewModel
{
    public string Title { get; set; } = string.Empty;
    public int MatchPercentage { get; set; }
    public string Detail { get; set; } = string.Empty;
}

public class ResetPasswordViewModel
{
    public string UserId { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;

    [Required, StringLength(100, MinimumLength = 8, ErrorMessage = "Password must be at least 8 characters.")]
    [RegularExpression(PasswordRules.Pattern, ErrorMessage = PasswordRules.Message)]
    [DataType(DataType.Password)]
    [Display(Name = "New password")]
    public string Password { get; set; } = string.Empty;

    [Required, Compare(nameof(Password), ErrorMessage = "Passwords do not match.")]
    [DataType(DataType.Password)]
    [Display(Name = "Confirm new password")]
    public string ConfirmPassword { get; set; } = string.Empty;

    /// <summary>Display-only (never bound): the account the link belongs to.</summary>
    [Microsoft.AspNetCore.Mvc.ModelBinding.BindNever]
    public string? Email { get; set; }

    [Microsoft.AspNetCore.Mvc.ModelBinding.BindNever]
    public bool LinkInvalid { get; set; }
}
