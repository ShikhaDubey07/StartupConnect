namespace StartupConnect.Models;

public enum TimeAvailability
{
    FullTime,
    PartTime,
    WeekendsOnly
}

public enum InvestmentCapacity
{
    None,
    UpTo10K,
    From10KTo50K,
    From50KTo1L,
    Above1L
}

public enum IdeaStatus
{
    Draft,
    Submitted,
    UnderReview,
    Approved,
    Rejected,
    /// <summary>Hidden by moderators after reports; the owner can edit and resubmit for review.</summary>
    Unpublished
}

public enum InterestType
{
    Work,
    Invest,
    Both
}

public enum InterestStatus
{
    Pending,
    Accepted,
    Rejected,
    Cancelled
}

public enum TeamStatus
{
    Forming,
    Active,
    Launched,
    Closed
}

public enum IdeaProgressStage
{
    Idea,
    Research,
    Prototype,
    MVP,
    Testing,
    Launch
}

/// <summary>
/// What a notification is about. Drives which user preference applies (see NotificationPreferences)
/// and the icon shown in the notification centre. Values are persisted — append only.
/// </summary>
public enum NotificationCategory
{
    System = 0,
    Interest = 1,
    Comment = 2,
    Like = 3,
    Connection = 4,
    Message = 5,
    Team = 6,
    Challenge = 7,
    Moderation = 8,
    Match = 9
}

/// <summary>Allowed values of <see cref="UserSettings.ProfileVisibility"/>.</summary>
public static class ProfileVisibilityOptions
{
    public const string Public = "Public";
    public const string RegisteredUsers = "RegisteredUsers";
    public const string Private = "Private";
}
