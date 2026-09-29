using Microsoft.EntityFrameworkCore;
using StartupConnect.Data;
using StartupConnect.Infrastructure;
using StartupConnect.Models;

namespace StartupConnect.Services;

public sealed class VerificationStatusInfo
{
    public bool IsVerified { get; init; }
    public FounderVerificationRequest? Latest { get; init; }
    public bool IsPending => !IsVerified && Latest?.Status == VerificationRequestStatus.Pending;
    public bool WasRejected => !IsVerified && Latest?.Status == VerificationRequestStatus.Rejected;
}

public sealed class ReportedIdeaGroup
{
    public int IdeaId { get; init; }
    public string IdeaTitle { get; init; } = string.Empty;
    public string OwnerName { get; init; } = string.Empty;
    public string OwnerId { get; init; } = string.Empty;
    public IdeaStatus IdeaStatus { get; init; }
    public int OpenCount { get; init; }
    public int TotalCount { get; init; }
    public DateTime LatestReportAt { get; init; }
    public List<IdeaReport> Reports { get; init; } = new();
}

public sealed class ModerationCounts
{
    public int PendingVerifications { get; init; }
    public int OpenReports { get; init; }
    public int ReportedIdeas { get; init; }
    public int ChallengesAwaitingResults { get; init; }
}

public interface IModerationService
{
    Task<VerificationStatusInfo> GetVerificationStatusAsync(string userId);
    Task<ServiceResult> RequestVerificationAsync(string userId, string? note, string? linkedInUrl);
    Task<List<FounderVerificationRequest>> GetPendingVerificationsAsync();
    Task<List<FounderVerificationRequest>> GetRecentVerificationDecisionsAsync(int count = 15);
    Task<ServiceResult> ApproveVerificationAsync(int requestId, string adminId);
    Task<ServiceResult> RejectVerificationAsync(int requestId, string? reason, string adminId);

    Task<List<ReportedIdeaGroup>> GetReportGroupsAsync(bool openOnly);
    Task<ServiceResult> DismissReportsAsync(int ideaId, int? reportId, string? note, string adminId);
    Task<ServiceResult> ResolveReportsAsync(int ideaId, string? note, string adminId);
    Task<ServiceResult> UnpublishIdeaAsync(int ideaId, string? reason, string adminId);

    Task<ModerationCounts> GetCountsAsync();
}

public sealed class ModerationService : IModerationService
{
    public const int MaxNote = 500;
    public const int MaxReason = 500;

    private readonly ApplicationDbContext _db;
    private readonly INotificationService _notifications;
    private readonly IActivityService _activity;

    public ModerationService(ApplicationDbContext db, INotificationService notifications, IActivityService activity)
    {
        _db = db;
        _notifications = notifications;
        _activity = activity;
    }

    // ---------------- Founder verification ----------------

    public async Task<VerificationStatusInfo> GetVerificationStatusAsync(string userId)
    {
        var verified = await _db.UserProfiles.Where(p => p.UserId == userId).Select(p => p.IsVerifiedFounder).FirstOrDefaultAsync();
        var latest = await _db.FounderVerificationRequests.AsNoTracking()
            .Where(r => r.UserId == userId)
            .OrderByDescending(r => r.CreatedAt)
            .FirstOrDefaultAsync();
        return new VerificationStatusInfo { IsVerified = verified, Latest = latest };
    }

    public async Task<ServiceResult> RequestVerificationAsync(string userId, string? note, string? linkedInUrl)
    {
        var status = await GetVerificationStatusAsync(userId);
        if (status.IsVerified) return ServiceResult.Fail("You're already a Verified Founder.");
        if (status.IsPending) return ServiceResult.Fail("Your verification request is already waiting for review.");

        note = note?.Trim() ?? string.Empty;
        linkedInUrl = string.IsNullOrWhiteSpace(linkedInUrl) ? null : linkedInUrl.Trim();
        if (note.Length < 20) return ServiceResult.Fail("Tell us a little about what you're building (at least 20 characters).");
        if (note.Length > MaxNote) return ServiceResult.Fail($"Keep your note under {MaxNote} characters.");
        if (linkedInUrl != null && (linkedInUrl.Length > 300 || !UrlSafety.IsSafeHttpUrl(linkedInUrl)))
            return ServiceResult.Fail("Please enter a valid LinkedIn URL starting with https://");

        var profile = await _db.UserProfiles.FirstOrDefaultAsync(p => p.UserId == userId);
        if (profile == null)
        {
            profile = new UserProfile { UserId = userId };
            _db.UserProfiles.Add(profile);
        }
        profile.VerificationRequested = true;
        if (linkedInUrl != null && string.IsNullOrEmpty(profile.LinkedInUrl)) profile.LinkedInUrl = linkedInUrl;

        _db.FounderVerificationRequests.Add(new FounderVerificationRequest
        {
            UserId = userId,
            Note = note,
            LinkedInUrl = linkedInUrl ?? profile.LinkedInUrl,
            Status = VerificationRequestStatus.Pending
        });
        await _db.SaveChangesAsync();
        return ServiceResult.Ok("Thanks! Your verification request was sent to our team. We'll notify you once it's reviewed.");
    }

    public Task<List<FounderVerificationRequest>> GetPendingVerificationsAsync() =>
        _db.FounderVerificationRequests.AsNoTracking()
            .Include(r => r.User).ThenInclude(u => u.Profile)
            .Where(r => r.Status == VerificationRequestStatus.Pending)
            .OrderBy(r => r.CreatedAt)
            .ToListAsync();

    public Task<List<FounderVerificationRequest>> GetRecentVerificationDecisionsAsync(int count = 15) =>
        _db.FounderVerificationRequests.AsNoTracking()
            .Include(r => r.User)
            .Include(r => r.ReviewedBy)
            .Where(r => r.Status != VerificationRequestStatus.Pending)
            .OrderByDescending(r => r.ReviewedAt)
            .Take(count)
            .ToListAsync();

    public async Task<ServiceResult> ApproveVerificationAsync(int requestId, string adminId)
    {
        var request = await _db.FounderVerificationRequests.Include(r => r.User).FirstOrDefaultAsync(r => r.Id == requestId);
        if (request == null) return ServiceResult.Fail("Request not found.");
        if (request.Status != VerificationRequestStatus.Pending) return ServiceResult.Fail("This request was already reviewed.");

        var profile = await _db.UserProfiles.FirstOrDefaultAsync(p => p.UserId == request.UserId);
        if (profile == null)
        {
            profile = new UserProfile { UserId = request.UserId };
            _db.UserProfiles.Add(profile);
        }
        profile.IsVerifiedFounder = true;
        profile.VerificationRequested = false;

        request.Status = VerificationRequestStatus.Approved;
        request.ReviewedByUserId = adminId;
        request.ReviewedAt = DateTime.UtcNow;
        await _activity.RecordAsync(request.UserId, ActivityTypes.VerifiedFounder,
            "You became a Verified Founder.", $"/Profile/Detail/{request.UserId}", save: false);
        await _db.SaveChangesAsync();

        await _notifications.CreateAsync(request.UserId, "You're a Verified Founder ✅",
            "Your founder verification was approved. The verified badge now shows on your profile and ideas.", "/Account/Profile", category: NotificationCategory.Moderation);
        return ServiceResult.Ok($"{request.User.FullName} is now a Verified Founder.");
    }

    public async Task<ServiceResult> RejectVerificationAsync(int requestId, string? reason, string adminId)
    {
        reason = reason?.Trim() ?? string.Empty;
        if (reason.Length == 0) return ServiceResult.Fail("Please give the applicant a reason.");
        if (reason.Length > MaxReason) return ServiceResult.Fail($"Reasons can be at most {MaxReason} characters.");

        var request = await _db.FounderVerificationRequests.Include(r => r.User).FirstOrDefaultAsync(r => r.Id == requestId);
        if (request == null) return ServiceResult.Fail("Request not found.");
        if (request.Status != VerificationRequestStatus.Pending) return ServiceResult.Fail("This request was already reviewed.");

        request.Status = VerificationRequestStatus.Rejected;
        request.RejectionReason = reason;
        request.ReviewedByUserId = adminId;
        request.ReviewedAt = DateTime.UtcNow;
        var profile = await _db.UserProfiles.FirstOrDefaultAsync(p => p.UserId == request.UserId);
        if (profile != null) profile.VerificationRequested = false;
        await _db.SaveChangesAsync();

        await _notifications.CreateAsync(request.UserId, "Founder verification update",
            $"Your verification request wasn't approved: {reason} You can update your details and apply again.", "/Account/Profile", category: NotificationCategory.Moderation);
        return ServiceResult.Ok($"Request from {request.User.FullName} was rejected and they've been notified.");
    }

    // ---------------- Idea reports ----------------

    public async Task<List<ReportedIdeaGroup>> GetReportGroupsAsync(bool openOnly)
    {
        var query = _db.IdeaReports.AsNoTracking()
            .Include(r => r.User)
            .Include(r => r.ResolvedBy)
            .Include(r => r.Idea).ThenInclude(i => i.Submitter)
            .AsQueryable();
        if (openOnly)
        {
            var openIdeaIds = _db.IdeaReports.Where(r => r.Status == ReportStatus.Open).Select(r => r.IdeaId);
            query = query.Where(r => openIdeaIds.Contains(r.IdeaId));
        }
        else
        {
            query = query.Where(r => !_db.IdeaReports.Any(o => o.IdeaId == r.IdeaId && o.Status == ReportStatus.Open));
        }

        var reports = await query.OrderByDescending(r => r.CreatedAt).Take(1000).ToListAsync();
        return reports
            .GroupBy(r => r.IdeaId)
            .Select(g => new ReportedIdeaGroup
            {
                IdeaId = g.Key,
                IdeaTitle = g.First().Idea.Title,
                OwnerName = g.First().Idea.Submitter.FullName,
                OwnerId = g.First().Idea.SubmitterUserId,
                IdeaStatus = g.First().Idea.Status,
                OpenCount = g.Count(r => r.Status == ReportStatus.Open),
                TotalCount = g.Count(),
                LatestReportAt = g.Max(r => r.CreatedAt),
                Reports = g.OrderBy(r => r.Status == ReportStatus.Open ? 0 : 1).ThenByDescending(r => r.CreatedAt).ToList()
            })
            .OrderByDescending(g => g.OpenCount)
            .ThenByDescending(g => g.LatestReportAt)
            .ToList();
    }

    private static string? CleanNote(string? note) =>
        string.IsNullOrWhiteSpace(note) ? null : note.Trim().Length > MaxNote ? note.Trim()[..MaxNote] : note.Trim();

    private async Task<int> CloseOpenReportsAsync(int ideaId, int? reportId, ReportStatus status, string? note, string adminId)
    {
        var query = _db.IdeaReports.Where(r => r.IdeaId == ideaId && r.Status == ReportStatus.Open);
        if (reportId.HasValue) query = query.Where(r => r.Id == reportId.Value);
        var now = DateTime.UtcNow;
        return await query.ExecuteUpdateAsync(s => s
            .SetProperty(r => r.Status, status)
            .SetProperty(r => r.ResolvedByUserId, adminId)
            .SetProperty(r => r.ResolvedAt, now)
            .SetProperty(r => r.ResolutionNote, note));
    }

    public async Task<ServiceResult> DismissReportsAsync(int ideaId, int? reportId, string? note, string adminId)
    {
        var count = await CloseOpenReportsAsync(ideaId, reportId, ReportStatus.Dismissed, CleanNote(note) ?? "No action needed", adminId);
        return count == 0
            ? ServiceResult.Fail("There were no open reports to dismiss.")
            : ServiceResult.Ok($"Dismissed {count} report{(count == 1 ? "" : "s")}.");
    }

    public async Task<ServiceResult> ResolveReportsAsync(int ideaId, string? note, string adminId)
    {
        var reporterIds = await _db.IdeaReports.Where(r => r.IdeaId == ideaId && r.Status == ReportStatus.Open)
            .Select(r => r.UserId).Distinct().ToListAsync();
        var count = await CloseOpenReportsAsync(ideaId, null, ReportStatus.Resolved, CleanNote(note) ?? "Resolved by moderators", adminId);
        if (count == 0) return ServiceResult.Fail("There were no open reports to resolve.");

        var title = await _db.Ideas.Where(i => i.Id == ideaId).Select(i => i.Title).FirstOrDefaultAsync() ?? "an idea";
        foreach (var reporterId in reporterIds)
        {
            await _notifications.CreateAsync(reporterId, "Update on your report",
                $"Thanks for flagging \"{title}\". Our moderators reviewed it and resolved the issue.", category: NotificationCategory.Moderation);
        }
        return ServiceResult.Ok($"Marked {count} report{(count == 1 ? "" : "s")} as resolved.");
    }

    public async Task<ServiceResult> UnpublishIdeaAsync(int ideaId, string? reason, string adminId)
    {
        reason = reason?.Trim() ?? string.Empty;
        if (reason.Length == 0) return ServiceResult.Fail("Please give the owner a reason for unpublishing.");
        if (reason.Length > MaxReason) return ServiceResult.Fail($"Reasons can be at most {MaxReason} characters.");

        var idea = await _db.Ideas.FirstOrDefaultAsync(i => i.Id == ideaId);
        if (idea == null) return ServiceResult.Fail("Idea not found.");
        if (idea.Status != IdeaStatus.Approved) return ServiceResult.Fail("Only published ideas can be unpublished.");

        idea.Status = IdeaStatus.Unpublished;
        idea.RejectionReason = reason;
        idea.UpdatedAt = DateTime.UtcNow;
        _db.ReviewNotes.Add(new ReviewNote { IdeaId = ideaId, AdminUserId = adminId, Note = $"Unpublished: {reason}" });
        await _db.SaveChangesAsync();

        var reporterIds = await _db.IdeaReports.Where(r => r.IdeaId == ideaId && r.Status == ReportStatus.Open)
            .Select(r => r.UserId).Distinct().ToListAsync();
        await CloseOpenReportsAsync(ideaId, null, ReportStatus.Resolved, $"Idea unpublished: {reason}", adminId);

        await _notifications.CreateAsync(idea.SubmitterUserId, "Your idea was unpublished",
            $"\"{idea.Title}\" was hidden by our moderators: {reason} Edit it to address this and resubmit it for review.", "/Ideas/MyIdeas", category: NotificationCategory.Moderation);
        foreach (var reporterId in reporterIds)
        {
            await _notifications.CreateAsync(reporterId, "Update on your report",
                $"Thanks for flagging \"{idea.Title}\". Our moderators have taken it down.", category: NotificationCategory.Moderation);
        }
        return ServiceResult.Ok($"\"{idea.Title}\" was unpublished and the owner has been notified.");
    }

    public async Task<ModerationCounts> GetCountsAsync()
    {
        var now = DateTime.UtcNow;
        return new ModerationCounts
        {
            PendingVerifications = await _db.FounderVerificationRequests.CountAsync(r => r.Status == VerificationRequestStatus.Pending),
            OpenReports = await _db.IdeaReports.CountAsync(r => r.Status == ReportStatus.Open),
            ReportedIdeas = await _db.IdeaReports.Where(r => r.Status == ReportStatus.Open).Select(r => r.IdeaId).Distinct().CountAsync(),
            ChallengesAwaitingResults = await _db.StartupChallenges.CountAsync(c =>
                c.ResultsAnnouncedAt == null && (!c.IsActive || c.Deadline <= now) && c.Submissions.Any())
        };
    }
}
