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
    private readonly IPrivacyService _privacy;
    private readonly IConnectionService _connections;

    public ProfileController(IProfileService profileService, UserManager<ApplicationUser> userManager, StartupConnect.Data.ApplicationDbContext context,
        IModerationService moderation, IActivityService activity, IPrivacyService privacy, IConnectionService connections)
    {
        _profileService = profileService;
        _userManager = userManager;
        _context = context;
        _moderation = moderation;
        _activity = activity;
        _privacy = privacy;
        _connections = connections;
    }

    /// <summary>Viewer id used when an owner previews their profile "as a signed-in member" (matches no one).</summary>
    private const string PreviewMemberViewerId = "preview-member";

    /// <summary>
    /// Public profile page. Visibility follows <see cref="IPrivacyService"/>; owners can preview it as a
    /// signed-out visitor (?preview=public) or as a signed-in member who isn't connected (?preview=member).
    /// </summary>
    [AllowAnonymous]
    public async Task<IActionResult> Detail(string id, string? preview = null)
    {
        if (string.IsNullOrEmpty(id)) return NotFound();

        var target = await _context.Users.AsNoTracking()
            .Where(u => u.Id == id)
            .Select(u => new { u.Id, u.FullName, u.Email, u.IsActive, PhotoUrl = u.Profile != null ? u.Profile.ProfilePhotoUrl : null })
            .FirstOrDefaultAsync();
        var viewerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var isAdmin = User.IsInRole("Admin");
        if (target == null || (!target.IsActive && !isAdmin)) return NotFound();

        var isOwner = viewerId == id;
        var previewMode = isOwner ? preview switch { "public" => "public", "member" => "member", _ => null } : null;
        var access = previewMode switch
        {
            "public" => await _privacy.GetAccessAsync(id, null, false),
            "member" => await _privacy.GetAccessAsync(id, PreviewMemberViewerId, false),
            _ => await _privacy.GetAccessAsync(id, viewerId, isAdmin)
        };
        var connectionState = viewerId != null && previewMode == null
            ? await _connections.GetStateAsync(viewerId, id)
            : previewMode == "member" ? ConnectionState.None : ConnectionState.Self;

        if (!access.CanView)
        {
            return View("Restricted", new PrivateProfileViewModel
            {
                UserId = id,
                FullName = target.FullName,
                PhotoUrl = target.PhotoUrl,
                SignInRequired = access.Reason == ProfileDenialReason.SignInRequired,
                ConnectionState = previewMode == "public" ? ConnectionState.Self : connectionState,
                IsOwnerPreview = previewMode != null
            });
        }

        var profile = await _profileService.GetProfileAsync(id);
        if (profile == null) return NotFound();

        // Apply the member's field-level privacy for this viewer.
        if (!access.ShowLocation) { profile.City = null; profile.State = null; }
        if (!access.ShowAge) profile.Age = null;
        ViewBag.ShownEmail = access.ShowEmail ? target.Email : null;
        ViewBag.ConnectionState = connectionState;
        ViewBag.PreviewMode = previewMode;
        ViewBag.ViaRelationship = access.ViaRelationship;
        if (isOwner)
        {
            ViewBag.PrivacyHint = new ProfilePrivacyHint
            {
                Visibility = access.Privacy.Visibility,
                ShowEmail = access.Privacy.ShowEmail,
                ShowLocation = access.Privacy.ShowLocation,
                ShowAge = access.Privacy.ShowAge,
                IsPreview = previewMode != null
            };
        }

        ViewBag.ProfileUserId = id;
        ViewBag.PhotoUrl = target.PhotoUrl;

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
