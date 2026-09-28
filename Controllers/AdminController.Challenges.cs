using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StartupConnect.Infrastructure;
using StartupConnect.Models;
using StartupConnect.Services;
using StartupConnect.ViewModels;

namespace StartupConnect.Controllers;

// Startup challenge management & judging (Admin / Panel).
public partial class AdminController
{
    [HttpGet]
    public async Task<IActionResult> Challenges()
    {
        var now = DateTime.UtcNow;
        var rows = await _context.StartupChallenges.AsNoTracking()
            .OrderByDescending(c => c.IsActive && c.Deadline > now)
            .ThenBy(c => c.Deadline)
            .Select(c => new AdminChallengeRowViewModel
            {
                Id = c.Id,
                Title = c.Title,
                Prize = c.Prize,
                DeadlineUtc = c.Deadline,
                IsActive = c.IsActive,
                IsOpen = c.IsActive && c.Deadline > now,
                ResultsAnnounced = c.ResultsAnnouncedAt != null,
                SubmissionCount = c.Submissions.Count(),
                WinnerCount = c.Submissions.Count(s => s.IsWinner),
                ShortlistCount = c.Submissions.Count(s => s.IsShortlisted),
                CategoryName = c.Category != null ? c.Category.Name : null
            })
            .ToListAsync();
        return View(rows);
    }

    private async Task LoadChallengeCategoriesAsync() =>
        ViewBag.Categories = await _context.Categories.Where(c => c.IsActive).OrderBy(c => c.Name).ToListAsync();

    [HttpGet]
    public async Task<IActionResult> CreateChallenge()
    {
        await LoadChallengeCategoriesAsync();
        var defaultDeadline = AppTime.ToIst(DateTime.UtcNow).Date.AddDays(30).AddHours(23).AddMinutes(59);
        return View("ChallengeForm", new ChallengeFormViewModel { DeadlineIst = defaultDeadline });
    }

    [HttpGet]
    public async Task<IActionResult> EditChallenge(int id)
    {
        var c = await _context.StartupChallenges.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (c == null) return NotFound();
        await LoadChallengeCategoriesAsync();
        return View("ChallengeForm", new ChallengeFormViewModel
        {
            Id = c.Id,
            Title = c.Title,
            Description = c.Description,
            Prize = c.Prize,
            DeadlineIst = AppTime.ToIst(c.Deadline),
            Eligibility = c.Eligibility,
            Rules = c.Rules,
            CoverImageUrl = c.CoverImageUrl,
            CategoryId = c.CategoryId,
            IsActive = c.IsActive,
            SubmissionCount = await _context.ChallengeSubmissions.CountAsync(s => s.ChallengeId == id)
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveChallenge(ChallengeFormViewModel model)
    {
        StartupChallenge? existing = null;
        if (model.Id.HasValue)
        {
            existing = await _context.StartupChallenges.FirstOrDefaultAsync(c => c.Id == model.Id.Value);
            if (existing == null) return NotFound();
        }

        if (!string.IsNullOrWhiteSpace(model.CoverImageUrl) && !UrlSafety.IsSafeHttpUrl(model.CoverImageUrl))
            ModelState.AddModelError(nameof(model.CoverImageUrl), "Use an http(s) image URL.");
        if (model.CategoryId.HasValue && !await _context.Categories.AnyAsync(c => c.Id == model.CategoryId.Value))
            ModelState.AddModelError(nameof(model.CategoryId), "Unknown category.");

        DateTime? deadlineUtc = model.DeadlineIst.HasValue ? AppTime.IstToUtc(model.DeadlineIst.Value) : null;
        var deadlineChanged = existing == null || (deadlineUtc.HasValue && Math.Abs((existing.Deadline - deadlineUtc.Value).TotalMinutes) >= 1);
        if (deadlineUtc.HasValue && deadlineChanged && model.IsActive && deadlineUtc.Value <= DateTime.UtcNow)
            ModelState.AddModelError(nameof(model.DeadlineIst), "An open challenge needs a deadline in the future.");

        if (!ModelState.IsValid)
        {
            await LoadChallengeCategoriesAsync();
            if (existing != null) model.SubmissionCount = await _context.ChallengeSubmissions.CountAsync(s => s.ChallengeId == existing.Id);
            return View("ChallengeForm", model);
        }

        var challenge = existing ?? new StartupChallenge { CreatedAt = DateTime.UtcNow };
        challenge.Title = model.Title.Trim();
        challenge.Description = model.Description.Trim();
        challenge.Prize = model.Prize.Trim();
        challenge.Deadline = deadlineUtc!.Value;
        challenge.Eligibility = string.IsNullOrWhiteSpace(model.Eligibility) ? null : model.Eligibility.Trim();
        challenge.Rules = string.IsNullOrWhiteSpace(model.Rules) ? null : model.Rules.Trim();
        challenge.CoverImageUrl = string.IsNullOrWhiteSpace(model.CoverImageUrl) ? null : model.CoverImageUrl.Trim();
        challenge.CategoryId = model.CategoryId;
        // Results are final: an announced challenge stays closed.
        challenge.IsActive = challenge.ResultsAnnounced ? false : model.IsActive;
        if (existing == null) _context.StartupChallenges.Add(challenge);
        await _context.SaveChangesAsync();

        TempData["Success"] = existing == null ? $"Challenge \"{challenge.Title}\" created." : "Challenge updated.";
        return RedirectToAction(nameof(Challenges));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleChallenge(int id)
    {
        var c = await _context.StartupChallenges.FirstOrDefaultAsync(x => x.Id == id);
        if (c == null) return NotFound();
        if (c.ResultsAnnounced)
        {
            TempData["Error"] = "Results were already announced — this challenge can't be reopened.";
            return RedirectToAction(nameof(Challenges));
        }
        if (!c.IsActive && c.Deadline <= DateTime.UtcNow)
        {
            TempData["Error"] = "The deadline has passed. Edit the challenge to set a new deadline and reopen it.";
            return RedirectToAction(nameof(EditChallenge), new { id });
        }
        c.IsActive = !c.IsActive;
        await _context.SaveChangesAsync();
        TempData["Success"] = c.IsActive ? $"\"{c.Title}\" is open again." : $"\"{c.Title}\" is closed to new submissions.";
        return RedirectToAction(nameof(Challenges));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteChallenge(int id)
    {
        var c = await _context.StartupChallenges
            .Include(x => x.Submissions).ThenInclude(s => s.Idea)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (c == null) return NotFound();

        var submitterIds = c.ResultsAnnounced ? new List<string>() : c.Submissions.Select(s => s.Idea.SubmitterUserId).Distinct().ToList();
        var title = c.Title;
        _context.StartupChallenges.Remove(c); // submissions cascade
        await _context.SaveChangesAsync();

        foreach (var userId in submitterIds)
        {
            await _notifications.CreateAsync(userId, "Challenge cancelled",
                $"The challenge \"{title}\" was cancelled by the organisers. Your ideas are unaffected.", "/Challenges");
        }
        TempData["Success"] = $"Challenge \"{title}\" deleted.";
        return RedirectToAction(nameof(Challenges));
    }

    [HttpGet]
    public async Task<IActionResult> JudgeChallenge(int id)
    {
        var c = await _context.StartupChallenges.AsNoTracking().Include(x => x.Category).FirstOrDefaultAsync(x => x.Id == id);
        if (c == null) return NotFound();

        var submissions = await _context.ChallengeSubmissions.AsNoTracking()
            .Where(s => s.ChallengeId == id)
            .OrderByDescending(s => s.IsWinner).ThenByDescending(s => s.IsShortlisted).ThenBy(s => s.SubmittedAt)
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
                IsWinner = s.IsWinner,
                IsShortlisted = s.IsShortlisted,
                AwardTitle = s.AwardTitle,
                JudgeFeedback = s.JudgeFeedback
            })
            .ToListAsync();

        return View(new AdminJudgeViewModel
        {
            Challenge = c,
            IsOpen = c.IsOpenForSubmissions(DateTime.UtcNow),
            Submissions = submissions
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> JudgeSubmission(int id, int challengeId, JudgeSubmissionInput input)
    {
        var belongs = await _context.ChallengeSubmissions.AnyAsync(s => s.Id == id && s.ChallengeId == challengeId);
        if (!belongs) return NotFound();
        if (await _context.StartupChallenges.AnyAsync(c => c.Id == challengeId && c.ResultsAnnouncedAt != null))
        {
            TempData["Error"] = "Results were already announced; judging is locked.";
            return RedirectToAction(nameof(JudgeChallenge), new { id = challengeId });
        }
        var result = await _challengeService.JudgeAsync(id, input);
        TempData[result.Succeeded ? "Success" : "Error"] = result.Message;
        return Redirect(Url.Action(nameof(JudgeChallenge), new { id = challengeId }) + $"#sub-{id}");
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AnnounceResults(int id)
    {
        var result = await _challengeService.AnnounceResultsAsync(id);
        TempData[result.Succeeded ? "Success" : "Error"] = result.Message;
        return RedirectToAction(nameof(JudgeChallenge), new { id });
    }
}
