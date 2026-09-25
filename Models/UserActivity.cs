namespace StartupConnect.Models;

public class UserActivity
{
    public int Id { get; set; }
    
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;

    public string ActionType { get; set; } = string.Empty; // e.g., "Created Idea", "Joined Team", "Left Comment"
    public string Description { get; set; } = string.Empty;
    public string? RelatedUrl { get; set; } 
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
