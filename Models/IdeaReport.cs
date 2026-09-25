namespace StartupConnect.Models;

public class IdeaReport
{
    public int Id { get; set; }
    public int IdeaId { get; set; }
    public Idea Idea { get; set; } = null!;
    
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;
    
    public string Reason { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
