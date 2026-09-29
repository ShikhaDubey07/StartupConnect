using StartupConnect.Services.Matching;

namespace StartupConnect.ViewModels;

/// <summary>Model of the shared <c>_TeamMatchCard</c> partial (Smart Matches + Find Team).</summary>
public sealed record TeamMatchCardModel(TeamMatch Match, ConnectionState State, string? AiRationale = null);

/// <summary>One collaboration/investment request on the Interests › Manage page.</summary>
public sealed class InterestRequestViewModel
{
    public int Id { get; init; }
    public int IdeaId { get; init; }
    public string IdeaTitle { get; init; } = string.Empty;
    public StartupConnect.Models.InterestType InterestType { get; init; }
    public StartupConnect.Models.InterestStatus Status { get; init; }
    public decimal? ProposedInvestmentAmount { get; init; }
    public List<string> SelectedRoles { get; init; } = new();
    public string? Message { get; init; }
    public DateTime CreatedAt { get; init; }
    public string? ResponseNote { get; init; }
    public DateTime? RespondedAt { get; init; }

    /// <summary>The other person: the sender for incoming requests, the idea owner for outgoing ones.</summary>
    public string OtherUserId { get; init; } = string.Empty;
    public string OtherName { get; init; } = string.Empty;
    public string? OtherPhotoUrl { get; init; }
    public string? OtherBio { get; init; }
    public bool OtherVerified { get; init; }
    public bool OtherIsInvestor { get; init; }
    public StartupConnect.Models.TimeAvailability? OtherAvailability { get; init; }
    public int? OtherHoursPerWeek { get; init; }
    public List<string> OtherSkills { get; init; } = new();
    public string? OtherLocation { get; set; }
    /// <summary>Whether the viewer may open the other person's full profile (privacy rules).</summary>
    public bool CanViewProfile { get; set; }
}
