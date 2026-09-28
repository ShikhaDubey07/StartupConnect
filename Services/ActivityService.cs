using Microsoft.EntityFrameworkCore;
using StartupConnect.Data;
using StartupConnect.Models;

namespace StartupConnect.Services;

/// <summary>Values stored in <see cref="UserActivity.ActionType"/>; the timeline picks icons/colours from them.</summary>
public static class ActivityTypes
{
    public const string IdeaSubmitted = "Idea Submitted";
    public const string IdeaApproved = "Idea Approved";
    public const string InterestReceived = "Interest Received";
    public const string JoinedTeam = "Joined Team";
    public const string TeamCreated = "Team Created";
    public const string LeftTeam = "Left Team";
    public const string MilestoneCreated = "Milestone Created";
    public const string MilestoneCompleted = "Milestone Completed";
    public const string ChallengeSubmitted = "Submitted to Challenge";
    public const string ChallengeResult = "Challenge Result";
    public const string VerifiedFounder = "Verified Founder";

    public static (string Icon, string Color) Style(string actionType) => actionType switch
    {
        IdeaSubmitted => ("bi-lightbulb", "primary"),
        IdeaApproved => ("bi-patch-check", "success"),
        InterestReceived => ("bi-hand-index", "warning"),
        JoinedTeam or TeamCreated => ("bi-people", "info"),
        LeftTeam => ("bi-box-arrow-left", "secondary"),
        MilestoneCreated => ("bi-flag", "primary"),
        MilestoneCompleted => ("bi-check2-circle", "success"),
        ChallengeSubmitted => ("bi-trophy", "info"),
        ChallengeResult => ("bi-award", "warning"),
        VerifiedFounder => ("bi-patch-check-fill", "success"),
        _ => ("bi-activity", "secondary")
    };
}

public interface IActivityService
{
    /// <summary>Adds an activity row; saved with the caller's next SaveChanges unless <paramref name="save"/> is true.</summary>
    Task RecordAsync(string userId, string actionType, string description, string? relatedUrl = null, bool save = true);
    Task<List<UserActivity>> GetRecentAsync(string userId, int count = 20);
}

public sealed class ActivityService : IActivityService
{
    private const int MaxDescription = 500;
    private readonly ApplicationDbContext _db;

    public ActivityService(ApplicationDbContext db) => _db = db;

    public async Task RecordAsync(string userId, string actionType, string description, string? relatedUrl = null, bool save = true)
    {
        if (string.IsNullOrEmpty(userId)) return;
        _db.UserActivities.Add(new UserActivity
        {
            UserId = userId,
            ActionType = actionType,
            Description = description.Length > MaxDescription ? description[..MaxDescription] : description,
            RelatedUrl = relatedUrl,
            CreatedAt = DateTime.UtcNow
        });
        if (save) await _db.SaveChangesAsync();
    }

    public Task<List<UserActivity>> GetRecentAsync(string userId, int count = 20) =>
        _db.UserActivities.AsNoTracking()
            .Where(a => a.UserId == userId)
            .OrderByDescending(a => a.CreatedAt)
            .Take(count)
            .ToListAsync();
}
