namespace StartupConnect.Models;

public class Idea
{
    public int Id { get; set; }
    public string SubmitterUserId { get; set; } = string.Empty;
    public ApplicationUser Submitter { get; set; } = null!;

    public string Title { get; set; } = string.Empty;
    public string Tagline { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ProblemStatement { get; set; } = string.Empty;
    public string Solution { get; set; } = string.Empty;
    public string TargetMarket { get; set; } = string.Empty;
    public string BusinessModel { get; set; } = string.Empty;


    public int CategoryId { get; set; }
    public Category Category { get; set; } = null!;

    public decimal MinimumFundRequired { get; set; }
    public int ExpectedTeamSize { get; set; } = 3;
    public IdeaStatus Status { get; set; } = IdeaStatus.Draft;
    public string? RejectionReason { get; set; }
    public string? DocumentUrl { get; set; }
    public DateTime? PublishedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public IdeaProgressStage ProgressStage { get; set; } = IdeaProgressStage.Idea;

    public ICollection<IdeaRoleNeeded> RolesNeeded { get; set; } = new List<IdeaRoleNeeded>();
    public ICollection<Interest> Interests { get; set; } = new List<Interest>();
    public Team? Team { get; set; }

    public ICollection<IdeaLike> Likes { get; set; } = new List<IdeaLike>();
    public ICollection<IdeaComment> Comments { get; set; } = new List<IdeaComment>();
    public ICollection<IdeaReport> Reports { get; set; } = new List<IdeaReport>();
    public ICollection<SavedIdea> SavedByUsers { get; set; } = new List<SavedIdea>();
    public ICollection<IdeaView> Views { get; set; } = new List<IdeaView>();
    
    public IdeaAnalysis? Analysis { get; set; }

    public ICollection<IdeaHistory> History { get; set; } = new List<IdeaHistory>();
    public ICollection<IdeaMilestone> Milestones { get; set; } = new List<IdeaMilestone>();
}

public class IdeaRoleNeeded
{
    public int Id { get; set; }
    public int IdeaId { get; set; }
    public Idea Idea { get; set; } = null!;
    public string RoleName { get; set; } = string.Empty;
}
