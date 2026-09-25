namespace StartupConnect.Models;

public class IdeaLike
{
    public int Id { get; set; }
    public int IdeaId { get; set; }
    public Idea Idea { get; set; } = null!;
    
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
