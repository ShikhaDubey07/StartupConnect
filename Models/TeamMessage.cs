namespace StartupConnect.Models;

public class TeamMessage
{
    public int Id { get; set; }
    
    public int TeamId { get; set; }
    public Team Team { get; set; } = null!;

    public string SenderUserId { get; set; } = string.Empty;
    public ApplicationUser Sender { get; set; } = null!;

    public string Content { get; set; } = string.Empty;
    public DateTime SentAt { get; set; } = DateTime.UtcNow;
}
