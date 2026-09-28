using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StartupConnect.Services;
using StartupConnect.ViewModels;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using StartupConnect.Models;
using StartupConnect.Infrastructure;

namespace StartupConnect.Controllers;

[Authorize]
public class ProfileController : Controller
{
    private readonly IProfileService _profileService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly StartupConnect.Data.ApplicationDbContext _context;

    private readonly IModerationService _moderation;
    private readonly IActivityService _activity;

    public ProfileController(IProfileService profileService, UserManager<ApplicationUser> userManager, StartupConnect.Data.ApplicationDbContext context,
        IModerationService moderation, IActivityService activity)
    {
        _profileService = profileService;
        _userManager = userManager;
        _context = context;
        _moderation = moderation;
        _activity = activity;
    }

    public async Task<IActionResult> Detail(string id)
    {
        if (string.IsNullOrEmpty(id)) return NotFound();

        var profile = await _profileService.GetProfileAsync(id);
        if (profile == null) return NotFound();

        ViewBag.ProfileUserId = id;

        // Fetch category names for interest tags
        var categoryIds = profile.SelectedCategoryIds;
        var categories = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync(
            _context.Categories.Where(c => categoryIds.Contains(c.Id)).Select(c => c.Name));
        ViewBag.CategoryNames = categories;

        // Fetch user's approved public ideas
        var userIdeas = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync(
            _context.Ideas
                .Include(i => i.Category)
                .Include(i => i.Interests)
                .Include(i => i.Likes)
                .Where(i => i.SubmitterUserId == id && i.Status == IdeaStatus.Approved));
        ViewBag.UserIdeas = userIdeas;
        ViewBag.IsVerifiedFounder = await _context.UserProfiles.Where(p => p.UserId == id).Select(p => p.IsVerifiedFounder).FirstOrDefaultAsync();

        return View(profile);
    }

    /// <summary>Founder analytics: real engagement totals across the user's ideas plus their activity timeline.</summary>
    public async Task<IActionResult> Analytics()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var ideas = await _context.Ideas.AsNoTracking()
            .Where(i => i.SubmitterUserId == userId)
            .Select(i => new FounderIdeaStat
            {
                IdeaId = i.Id,
                Title = i.Title,
                Status = i.Status,
                Views = i.Views.Count(),
                Likes = i.Likes.Count(),
                Comments = i.Comments.Count(),
                Interests = i.Interests.Count()
            })
            .ToListAsync();

        var model = new FounderAnalyticsViewModel
        {
            IdeaCount = ideas.Count,
            TotalViews = ideas.Sum(i => i.Views),
            TotalLikes = ideas.Sum(i => i.Likes),
            TotalComments = ideas.Sum(i => i.Comments),
            TotalInterests = ideas.Sum(i => i.Interests),
            AverageEngagement = ideas.Count == 0 ? 0 : Math.Round(ideas.Average(i => i.Score), 1),
            Ideas = ideas.OrderByDescending(i => i.Score).ThenBy(i => i.Title).ToList(),
            Activities = await _activity.GetRecentAsync(userId, 25),
            MilestonesCompleted = await _context.UserActivities.CountAsync(a => a.UserId == userId && a.ActionType == ActivityTypes.MilestoneCompleted),
            ChallengeEntries = await _context.ChallengeSubmissions.CountAsync(s => s.Idea.SubmitterUserId == userId)
        };
        return View(model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    [RequireConfirmedEmail]
    public async Task<IActionResult> RequestVerification(string? note, string? linkedInUrl)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var result = await _moderation.RequestVerificationAsync(userId, note, linkedInUrl);
        TempData[result.Succeeded ? "Success" : "Error"] = result.Message;
        if (!result.Succeeded) TempData["VerificationNoteDraft"] = note?.Length > 500 ? note[..500] : note;
        return Redirect(Url.Action("Profile", "Account") + "#verification");
    }
}
