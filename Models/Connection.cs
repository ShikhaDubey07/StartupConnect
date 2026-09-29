namespace StartupConnect.Models;

public enum ConnectionStatus
{
    Pending,
    Accepted,
    Declined,
    Withdrawn
}

/// <summary>
/// A professional connection between two members. There is at most one row per pair of users
/// (enforced by the unique index on <see cref="UserAId"/>/<see cref="UserBId"/>, the ordinally
/// sorted pair); re-sending after a decline/withdrawal reuses the row. Removing a connection deletes it.
/// Accepted connections can see each other's Private profiles (and, from Session 5, message each other).
/// </summary>
public class Connection
{
    public const int MaxMessageLength = 300;

    public int Id { get; set; }

    public string RequesterId { get; set; } = string.Empty;
    public ApplicationUser Requester { get; set; } = null!;

    public string AddresseeId { get; set; } = string.Empty;
    public ApplicationUser Addressee { get; set; } = null!;

    /// <summary>Ordinal min of (RequesterId, AddresseeId) — with <see cref="UserBId"/> makes the pair unique.</summary>
    public string UserAId { get; set; } = string.Empty;
    /// <summary>Ordinal max of (RequesterId, AddresseeId).</summary>
    public string UserBId { get; set; } = string.Empty;

    public ConnectionStatus Status { get; set; } = ConnectionStatus.Pending;
    public string? Message { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? RespondedAt { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public void SetPair(string requesterId, string addresseeId)
    {
        RequesterId = requesterId;
        AddresseeId = addresseeId;
        (UserAId, UserBId) = Order(requesterId, addresseeId);
    }

    public string OtherUserId(string userId) => RequesterId == userId ? AddresseeId : RequesterId;

    public static (string A, string B) Order(string x, string y) =>
        string.CompareOrdinal(x, y) <= 0 ? (x, y) : (y, x);
}
