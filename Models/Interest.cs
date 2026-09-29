namespace StartupConnect.Models;

public class Interest
{
    public int Id { get; set; }
    public int IdeaId { get; set; }
    public Idea Idea { get; set; } = null!;
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;

    public InterestType InterestType { get; set; }
    public decimal? ProposedInvestmentAmount { get; set; }
    public string? SelectedRoles { get; set; }
    public string? Message { get; set; }
    public InterestStatus Status { get; set; } = InterestStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class Team
{
    public int Id { get; set; }
    public int IdeaId { get; set; }
    public Idea Idea { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public TeamStatus Status { get; set; } = TeamStatus.Forming;
    public decimal TotalPledgedAmount { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<TeamMember> Members { get; set; } = new List<TeamMember>();
}

public static class TeamRoles
{
    /// <summary>Role label of the idea owner's membership row. The founder can't be removed or leave.</summary>
    public const string Founder = "Founder";
    public const int MaxLength = 60;

    /// <summary>Suggestions shown in the role picker (free text up to <see cref="MaxLength"/> is allowed).</summary>
    public static readonly string[] Suggested =
        ["Co-founder", "Developer", "Designer", "Marketing", "Sales", "Operations", "Finance", "Content Creator", "Advisor", "Investor", "Member"];
}

public class TeamMember
{
    public int Id { get; set; }
    public int TeamId { get; set; }
    public Team Team { get; set; } = null!;
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;
    public string Role { get; set; } = string.Empty;
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
}

public class Notification
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public bool IsRead { get; set; }
    public string? LinkUrl { get; set; }
    public NotificationCategory Category { get; set; } = NotificationCategory.System;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class ReviewNote
{
    public int Id { get; set; }
    public int IdeaId { get; set; }
    public Idea Idea { get; set; } = null!;
    public string AdminUserId { get; set; } = string.Empty;
    public string Note { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class ContactMessage
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsResolved { get; set; }
}
