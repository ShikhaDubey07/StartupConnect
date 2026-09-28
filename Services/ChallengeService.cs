using Microsoft.EntityFrameworkCore;
using StartupConnect.Data;
using StartupConnect.Models;

namespace StartupConnect.Services;

public sealed class JudgeSubmissionInput
{
    public bool IsShortlisted { get; set; }
    public bool IsWinner { get; set; }
    public string? AwardTitle { get; set; }
    public string? JudgeFeedback { get; set; }
}

public interface IChallengeService
{
    Task<ServiceResult> SubmitAsync(int challengeId, int ideaId, string? pitchNotes, string userId);
    Task<ServiceResult> WithdrawAsync(int submissionId, string userId);
    Task<ServiceResult> JudgeAsync(int submissionId, JudgeSubmissionInput input);
    /// <summary>Closes the challenge, publishes winners and notifies every submitter of their result.</summary>
    Task<ServiceResult> AnnounceResultsAsync(int challengeId);
}

/// <summary>
/// Challenge rules: only the owner of an APPROVED idea can enter it; one entry per idea per challenge;
/// entries (and withdrawals) only while the challenge is active and before the deadline. Judging
/// (shortlist/winner) is admin-only and stays private until results are announced.
/// </summary>
public sealed class ChallengeService : IChallengeService
{
    public const int MaxPitchNotes = 2000;
    public const int MaxAwardTitle = 60;
    public const int MaxJudgeFeedback = 1000;

    private readonly ApplicationDbContext _db;
    private readonly INotificationService _notifications;
    private readonly IActivityService _activity;

    public ChallengeService(ApplicationDbContext db, INotificationService notifications, IActivityService activity)
    {
        _db = db;
        _notifications = notifications;
        _activity = activity;
    }

    public async Task<ServiceResult> SubmitAsync(int challengeId, int ideaId, string? pitchNotes, string userId)
    {
        var challenge = await _db.StartupChallenges.FirstOrDefaultAsync(c => c.Id == challengeId);
        if (challenge == null) return ServiceResult.Fail("That challenge doesn't exist.");
        if (!challenge.IsActive) return ServiceResult.Fail("This challenge is closed and no longer accepts submissions.");
        if (challenge.Deadline <= DateTime.UtcNow) return ServiceResult.Fail("The deadline for this challenge has passed.");

        var idea = await _db.Ideas.FirstOrDefaultAsync(i => i.Id == ideaId && i.SubmitterUserId == userId);
        if (idea == null) return ServiceResult.Fail("Please choose one of your own ideas.");
        if (idea.Status != IdeaStatus.Approved) return ServiceResult.Fail("Only approved (published) ideas can be entered into challenges.");

        pitchNotes = pitchNotes?.Trim() ?? string.Empty;
        if (pitchNotes.Length == 0) return ServiceResult.Fail("Add a short pitch explaining why your idea fits this challenge.");
        if (pitchNotes.Length > MaxPitchNotes) return ServiceResult.Fail($"Pitch notes can be at most {MaxPitchNotes} characters.");

        if (await _db.ChallengeSubmissions.AnyAsync(s => s.ChallengeId == challengeId && s.IdeaId == ideaId))
            return ServiceResult.Fail($"\"{idea.Title}\" is already entered in this challenge.");

        _db.ChallengeSubmissions.Add(new ChallengeSubmission
        {
            ChallengeId = challengeId,
            IdeaId = ideaId,
            PitchNotes = pitchNotes,
            SubmittedAt = DateTime.UtcNow
        });
        await _activity.RecordAsync(userId, ActivityTypes.ChallengeSubmitted,
            $"You submitted \"{idea.Title}\" to {challenge.Title}.", $"/Challenges/Details/{challengeId}", save: false);
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Unique index (ChallengeId, IdeaId) caught a double submit.
            return ServiceResult.Fail($"\"{idea.Title}\" is already entered in this challenge.");
        }

        return ServiceResult.Ok($"\"{idea.Title}\" was submitted to {challenge.Title}. Good luck!");
    }

    public async Task<ServiceResult> WithdrawAsync(int submissionId, string userId)
    {
        var submission = await _db.ChallengeSubmissions
            .Include(s => s.Challenge)
            .Include(s => s.Idea)
            .FirstOrDefaultAsync(s => s.Id == submissionId);
        if (submission == null || submission.Idea.SubmitterUserId != userId)
            return ServiceResult.Fail("We couldn't find that submission.");
        if (!submission.Challenge.IsOpenForSubmissions(DateTime.UtcNow))
            return ServiceResult.Fail("Submissions can only be withdrawn before the deadline while the challenge is open.");

        _db.ChallengeSubmissions.Remove(submission);
        await _db.SaveChangesAsync();
        return ServiceResult.Ok($"\"{submission.Idea.Title}\" was withdrawn from the challenge.");
    }

    public async Task<ServiceResult> JudgeAsync(int submissionId, JudgeSubmissionInput input)
    {
        var submission = await _db.ChallengeSubmissions.Include(s => s.Idea).FirstOrDefaultAsync(s => s.Id == submissionId);
        if (submission == null) return ServiceResult.Fail("Submission not found.");

        var award = input.AwardTitle?.Trim();
        var feedback = input.JudgeFeedback?.Trim();
        if (award?.Length > MaxAwardTitle) return ServiceResult.Fail($"Award titles can be at most {MaxAwardTitle} characters.");
        if (feedback?.Length > MaxJudgeFeedback) return ServiceResult.Fail($"Feedback can be at most {MaxJudgeFeedback} characters.");

        submission.IsWinner = input.IsWinner;
        // Winners are implicitly shortlisted.
        submission.IsShortlisted = input.IsShortlisted || input.IsWinner;
        submission.AwardTitle = input.IsWinner ? (string.IsNullOrEmpty(award) ? "Winner" : award) : null;
        submission.JudgeFeedback = string.IsNullOrEmpty(feedback) ? null : feedback;
        await _db.SaveChangesAsync();
        return ServiceResult.Ok($"Saved judging for \"{submission.Idea.Title}\".");
    }

    /// <summary>"won Jury's Choice in X" / "won X" (when the award is just "Winner").</summary>
    public static string WonPhrase(string? award, string challengeTitle) =>
        string.IsNullOrWhiteSpace(award) || award.Equals("Winner", StringComparison.OrdinalIgnoreCase)
            ? $"won {challengeTitle}"
            : $"won {award} in {challengeTitle}";

    public async Task<ServiceResult> AnnounceResultsAsync(int challengeId)
    {
        var challenge = await _db.StartupChallenges
            .Include(c => c.Submissions).ThenInclude(s => s.Idea)
            .FirstOrDefaultAsync(c => c.Id == challengeId);
        if (challenge == null) return ServiceResult.Fail("Challenge not found.");
        if (challenge.ResultsAnnounced) return ServiceResult.Fail("Results for this challenge were already announced.");
        if (challenge.Submissions.Count == 0) return ServiceResult.Fail("There are no submissions to announce results for.");
        if (!challenge.Submissions.Any(s => s.IsWinner)) return ServiceResult.Fail("Mark at least one winner before announcing results.");

        challenge.IsActive = false;
        challenge.ResultsAnnouncedAt = DateTime.UtcNow;
        if (challenge.Deadline > DateTime.UtcNow) challenge.Deadline = DateTime.UtcNow;

        var link = $"/Challenges/Details/{challenge.Id}";
        foreach (var s in challenge.Submissions)
        {
            var (title, message) = s.IsWinner
                ? ("You won! 🏆", $"\"{s.Idea.Title}\" {WonPhrase(s.AwardTitle, challenge.Title)}. Congratulations!")
                : s.IsShortlisted
                    ? ("Challenge results: shortlisted", $"\"{s.Idea.Title}\" was shortlisted in {challenge.Title}. Well done — see the winners.")
                    : ("Challenge results announced", $"Results for {challenge.Title} are out. \"{s.Idea.Title}\" wasn't selected this time — thanks for taking part!");
            if (!string.IsNullOrEmpty(s.JudgeFeedback)) message += $" Judges' feedback: {s.JudgeFeedback}";

            if (s.IsWinner || s.IsShortlisted)
            {
                await _activity.RecordAsync(s.Idea.SubmitterUserId, ActivityTypes.ChallengeResult,
                    s.IsWinner
                        ? $"\"{s.Idea.Title}\" {WonPhrase(s.AwardTitle, challenge.Title)}."
                        : $"\"{s.Idea.Title}\" was shortlisted in {challenge.Title}.",
                    link, save: false);
            }
            await _notifications.CreateAsync(s.Idea.SubmitterUserId, title, message, link);
        }
        await _db.SaveChangesAsync();
        return ServiceResult.Ok($"Results announced — {challenge.Submissions.Count} submitter(s) notified.");
    }
}
