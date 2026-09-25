namespace StartupConnect.Models;

public class IdeaHistory
{
    public int Id { get; set; }
    
    public int IdeaId { get; set; }
    public Idea Idea { get; set; } = null!;

    public string? EditorId { get; set; }
    public ApplicationUser? Editor { get; set; }

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
    public int ExpectedTeamSize { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
