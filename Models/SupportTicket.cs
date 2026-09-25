namespace StartupConnect.Models;

public class SupportTicket
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;
    
    public string IssueCategory { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    
    public TicketStatus Status { get; set; } = TicketStatus.Open;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<SupportTicketMessage> Messages { get; set; } = new List<SupportTicketMessage>();
}

public class SupportTicketMessage
{
    public int Id { get; set; }
    public int SupportTicketId { get; set; }
    public SupportTicket Ticket { get; set; } = null!;
    
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;
    
    public string Message { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public enum TicketStatus
{
    Open,
    InProgress,
    Resolved,
    Closed
}
