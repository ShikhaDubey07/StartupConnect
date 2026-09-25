using Microsoft.AspNetCore.Identity;

namespace StartupConnect.Models;

public class ApplicationUser : IdentityUser
{
    public string FullName { get; set; } = string.Empty;
    public string? City { get; set; }
    public string? State { get; set; }
    public int? Age { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;

    public UserProfile? Profile { get; set; }
    public UserSettings? Settings { get; set; }
    public ICollection<Idea> SubmittedIdeas { get; set; } = new List<Idea>();
    public ICollection<Interest> Interests { get; set; } = new List<Interest>();
    public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
    public ICollection<IdeaLike> IdeaLikes { get; set; } = new List<IdeaLike>();
    public ICollection<IdeaComment> IdeaComments { get; set; } = new List<IdeaComment>();
    public ICollection<IdeaReport> IdeaReports { get; set; } = new List<IdeaReport>();
    public ICollection<SavedIdea> SavedIdeas { get; set; } = new List<SavedIdea>();
    public ICollection<SupportTicket> SupportTickets { get; set; } = new List<SupportTicket>();
}
