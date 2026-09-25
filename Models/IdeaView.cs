namespace StartupConnect.Models;

public class IdeaView
{
    public int Id { get; set; }
    public int IdeaId { get; set; }
    public Idea Idea { get; set; } = null!;

    public string? UserId { get; set; }
    public ApplicationUser? User { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
