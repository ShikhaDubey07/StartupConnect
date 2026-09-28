using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StartupConnect.Data;
using StartupConnect.Infrastructure;
using StartupConnect.Models;
using StartupConnect.Services;
using StartupConnect.ViewModels;

namespace StartupConnect.Controllers;

public class ChallengesController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly IChallengeService _challenges;

    public ChallengesController(ApplicationDbContext context, IChallengeService challenges)
    {
        _context = context;
        _challenges = challenges;
    }

    private string? CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier);
    private bool IsJudge => User.IsInRole("Admin") || User.IsInRole("Panel");

    public async Task<IActionResult> Index(string? tab = null)
    {
        var now = DateTime.UtcNow;
        var userId = CurrentUserId;
        var challenges = await _context.StartupChallenges.AsNoTracking()
            .Select(c => new
            {
                c.Id, c.Title, c.Description, c.Prize, c.Deadline, c.IsActive, c.ResultsAnnouncedAt, c.CoverImageUrl,
                CategoryName = c.Category != null ? c.Category.Name : null,
                CategoryIcon = c.Category != null ? c.Category.IconClass : null,
                SubmissionCount = c.Submissions.Count(),
                Winners = c.ResultsAnnouncedAt != null
                    ? c.Submissions.Where(s => s.IsWinner).Select(s => s.Idea.Title).ToList()
                    : new List<string>(),
                Mine = userId != null && c.Submissions.Any(s => s.Idea.SubmitterUserId == userId)
            })
            .ToListAsync();

        var cards = challenges.Select(c => new ChallengeCardViewModel
        {
            Id = c.Id,
            Title = c.Title,
            Description = c.Description,
            Prize = c.Prize,
            DeadlineUtc = c.Deadline,
            IsActive = c.IsActive,
            IsOpen = c.IsActive && c.Deadline > now,
            ResultsAnnounced = c.ResultsAnnouncedAt.HasValue,
            CategoryName = c.CategoryName,
            CategoryIcon = c.CategoryIcon,
            CoverImageUrl = UrlSafety.IsSafeHttpUrl(c.CoverImageUrl) ? c.CoverImageUrl : null,
            SubmissionCount = c.SubmissionCount,
            WinnerIdeaTitles = c.Winners,
            HasMySubmission = c.Mine
        }).ToList();

        var model = new ChallengeIndexViewModel
        {
            Tab = string.Equals(tab, "past", StringComparison.OrdinalIgnoreCase) ? "past" : "active",
            Active = cards.Where(c => c.IsOpen).OrderBy(c => c.DeadlineUtc).ToList(),
            Past = cards.Where(c => !c.IsOpen).OrderByDescending(c => c.DeadlineUtc).ToList()
        };
        return View(model);
    }

    public async Task<IActionResult> Details(int id)
    {
        var challenge = await _context.StartupChallenges.AsNoTracking()
            .Include(c => c.Category)
            .FirstOrDefaultAsync(c => c.Id == id);
        if (challenge == null) return NotFound();

        var userId = CurrentUserId;
        var submissions = await _context.ChallengeSubmissions.AsNoTracking()
            .Where(s => s.ChallengeId == id)
            .OrderBy(s => s.SubmittedAt)
            .Select(s => new ChallengeSubmissionRowViewModel
            {
                Id = s.Id,
                IdeaId = s.IdeaId,
                IdeaTitle = s.Idea.Title,
                IdeaTagline = s.Idea.Tagline,
                IdeaIsPublic = s.Idea.Status == IdeaStatus.Approved,
                FounderId = s.Idea.SubmitterUserId,
                FounderName = s.Idea.Submitter.FullName,
                FounderVerified = s.Idea.Submitter.Profile != null && s.Idea.Submitter.Profile.IsVerifiedFounder,
                SubmittedAt = s.SubmittedAt,
                PitchNotes = s.PitchNotes,
                IsMine = userId != null && s.Idea.SubmitterUserId == userId,
                IsWinner = s.IsWinner,
                IsShortlisted = s.IsShortlisted,
                AwardTitle = s.AwardTitle,
                JudgeFeedback = s.JudgeFeedback
            })
            .ToListAsync();

        var model = new ChallengeDetailsViewModel
        {
            Challenge = challenge,
            IsOpen = challenge.IsOpenForSubmissions(DateTime.UtcNow),
            IsAuthenticated = userId != null,
            IsAdmin = IsJudge,
            // Public list: entries whose idea is still published (owners always see their own).
            Submissions = submissions.Where(s => s.IdeaIsPublic || s.IsMine || IsJudge).ToList(),
            MySubmissions = submissions.Where(s => s.IsMine).ToList()
        };

        if (challenge.ResultsAnnounced)
        {
            model.Winners = submissions.Where(s => s.IsWinner && s.IdeaIsPublic).ToList();
            model.Shortlisted = submissions.Where(s => s.IsShortlisted && !s.IsWinner && s.IdeaIsPublic).ToList();
        }

        if (userId != null)
        {
            model.EmailConfirmed = await _context.Users.Where(u => u.Id == userId).Select(u => u.EmailConfirmed).FirstOrDefaultAsync();
            var enteredIdeaIds = model.MySubmissions.Select(s => s.IdeaId).ToHashSet();
            var myIdeas = await _context.Ideas.AsNoTracking()
                .Where(i => i.SubmitterUserId == userId)
                .Select(i => new { i.Id, i.Title, i.Status })
                .ToListAsync();
            model.MyApprovedIdeaCount = myIdeas.Count(i => i.Status == IdeaStatus.Approved);
            model.MyPendingIdeaCount = myIdeas.Count(i => i.Status is IdeaStatus.Submitted or IdeaStatus.UnderReview);
            model.EligibleIdeas = myIdeas
                .Where(i => i.Status == IdeaStatus.Approved && !enteredIdeaIds.Contains(i.Id))
                .OrderBy(i => i.Title)
                .Select(i => (i.Id, i.Title))
                .ToList();
        }

        return View(model);
    }

    [Authorize]
    [RequireConfirmedEmail]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SubmitIdea(int challengeId, int ideaId, string? pitchNotes)
    {
        var result = await _challenges.SubmitAsync(challengeId, ideaId, pitchNotes, CurrentUserId!);
        TempData[result.Succeeded ? "Success" : "Error"] = result.Message;
        if (!result.Succeeded) TempData["PitchDraft"] = pitchNotes?.Length > ChallengeService.MaxPitchNotes ? pitchNotes[..ChallengeService.MaxPitchNotes] : pitchNotes;
        return RedirectToAction(nameof(Details), "Challenges", new { id = challengeId }, result.Succeeded ? "submissions" : "submit");
    }

    [Authorize]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Withdraw(int id, int challengeId)
    {
        var result = await _challenges.WithdrawAsync(id, CurrentUserId!);
        TempData[result.Succeeded ? "Success" : "Error"] = result.Message;
        return RedirectToAction(nameof(Details), new { id = challengeId });
    }
}
