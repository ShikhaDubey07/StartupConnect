namespace StartupConnect.Models;

public class IdeaMilestone
{
    public int Id { get; set; }
    
    public int IdeaId { get; set; }
    public Idea Idea { get; set; } = null!;

    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime? DueDate { get; set; }
    public bool IsCompleted { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
