namespace StartupConnect.Models;

public class SavedIdea
{
    public int Id { get; set; }
    
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;
    
    public int IdeaId { get; set; }
    public Idea Idea { get; set; } = null!;
    
    public DateTime SavedAt { get; set; } = DateTime.UtcNow;
}
