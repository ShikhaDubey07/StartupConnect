using StartupConnect.Models;

namespace StartupConnect.Services.Matching;

/// <summary>A scored co-founder / team candidate, ready for display (location already privacy-filtered).</summary>
public sealed class TeamMatch
{
    public string UserId { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
    public string? PhotoUrl { get; init; }
    public string? Bio { get; init; }
    public string? Location { get; init; }
    public bool IsInvestor { get; init; }
    public bool IsVerified { get; init; }
    public TimeAvailability Availability { get; init; }
    public int HoursPerWeek { get; init; }
    public List<string> Skills { get; init; } = new();
    public List<string> Interests { get; init; } = new();
    public TeamMatchScore Match { get; init; } = new();
    public int Score => Match.Score;
}

/// <summary>Filters for Find Team. <see cref="ForIdeaId"/> switches to "for idea" mode.</summary>
public sealed class TeamSearch
{
    public string? Role { get; init; }
    public string? Skill { get; init; }
    public int? IndustryId { get; init; }
    public TimeAvailability? Availability { get; init; }
    public int? ForIdeaId { get; init; }
    public bool IncludeConnections { get; init; }
}

/// <summary>An idea of the seeker that can be used for "for idea" mode.</summary>
public sealed record SeekerIdea(int Id, string Title, IdeaStatus Status, List<string> RolesNeeded, List<OpenRoleNeed> OpenNeeds, List<string> FilledRoles);

public sealed class TeamMatchResults
{
    public List<TeamMatch> Matches { get; init; } = new();
    /// <summary>False when the seeker has no profile yet (nothing to match on).</summary>
    public bool HasProfile { get; init; } = true;
    /// <summary>Open role needs used for ranking (all ideas, or the selected idea).</summary>
    public List<OpenRoleNeed> OpenNeeds { get; init; } = new();
    /// <summary>The seeker's own ideas (for the "for idea" picker).</summary>
    public List<SeekerIdea> MyIdeas { get; init; } = new();
    /// <summary>Set in "for idea" mode.</summary>
    public SeekerIdea? ForIdea { get; init; }
    /// <summary>People left out because they are already connections or teammates.</summary>
    public int NetworkExcluded { get; init; }
}

/// <summary>A similar idea with display data and human-readable reasons.</summary>
public sealed class SimilarIdeaMatch
{
    public int IdeaId { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Tagline { get; init; } = string.Empty;
    public string CategoryName { get; init; } = string.Empty;
    public string CategoryIcon { get; init; } = "bi-lightbulb";
    public string SubmitterId { get; init; } = string.Empty;
    public string SubmitterName { get; init; } = string.Empty;
    public bool SubmitterVerified { get; init; }
    public decimal MinimumFundRequired { get; init; }
    public int Score { get; init; }
    public bool SameCategory { get; init; }
    public bool SimilarMarket { get; init; }
    public List<string> CommonKeywords { get; init; } = new();

    public IEnumerable<string> Reasons
    {
        get
        {
            if (SameCategory) yield return $"Same category: {CategoryName}";
            if (SimilarMarket) yield return "Similar target market";
            if (CommonKeywords.Count > 0) yield return "Shared topics: " + string.Join(", ", CommonKeywords.Take(4));
        }
    }
}
