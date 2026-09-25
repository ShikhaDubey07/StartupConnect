using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StartupConnect.Services;
using System.Security.Claims;

namespace StartupConnect.Controllers;

[Authorize]
public class MatchesController : Controller
{
    private readonly IMatchingService _matchingService;
    private readonly IAIAnalysisService _aiService;

    public MatchesController(IMatchingService matchingService, IAIAnalysisService aiService)
    {
        _matchingService = matchingService;
        _aiService = aiService;
    }

    public async Task<IActionResult> Index()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

        var coFounders = await _matchingService.GetCoFounderMatchesAsync(userId);
        var investors  = await _matchingService.GetRecommendedInvestorsAsync(userId);
        var ideas      = await _matchingService.GetRecommendedIdeasForInvestmentAsync(userId);

        ViewBag.CoFounders    = coFounders;
        ViewBag.Investors     = investors;
        ViewBag.IdeasToInvest = ideas;

        // --- AI: co-founder match rationales (top 5, cached per pair) ---
        var rationaleDict = new Dictionary<string, string>();
        var rationaleTop5 = coFounders.Take(5).ToList();
        if (rationaleTop5.Any())
        {
            var rationaleTasks = rationaleTop5.Select(async m =>
            {
                try
                {
                    var r = await _aiService.GetCoFounderRationaleAsync(userId, m.Profile.UserId);
                    return (m.Profile.UserId, Rationale: r);
                }
                catch
                {
                    return (m.Profile.UserId, Rationale: string.Empty);
                }
            });

            var rationaleResults = await Task.WhenAll(rationaleTasks);
            foreach (var (profileUserId, rationale) in rationaleResults)
            {
                if (!string.IsNullOrWhiteSpace(rationale))
                    rationaleDict[profileUserId] = rationale;
            }
        }
        ViewBag.CoFounderRationales = rationaleDict;

        // --- AI: investor pitch summaries (top 5 investment ideas, cached per idea) ---
        var pitchDict = new Dictionary<int, string>();
        var pitchTop5 = ideas.Take(5).ToList();
        if (pitchTop5.Any())
        {
            var pitchTasks = pitchTop5.Select(async idea =>
            {
                try
                {
                    var p = await _aiService.GenerateInvestorPitchAsync(idea.Id);
                    return (idea.Id, Pitch: p ?? string.Empty);
                }
                catch
                {
                    return (idea.Id, Pitch: string.Empty);
                }
            });

            var pitchResults = await Task.WhenAll(pitchTasks);
            foreach (var (ideaId, pitch) in pitchResults)
            {
                if (!string.IsNullOrWhiteSpace(pitch))
                    pitchDict[ideaId] = pitch;
            }
        }
        ViewBag.InvestorPitches = pitchDict;

        return View();
    }

    [HttpGet]
    public async Task<IActionResult> FindTeam([FromQuery] StartupConnect.ViewModels.FindTeamViewModel filter, [FromServices] StartupConnect.Data.ApplicationDbContext context)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        
        // Populate Categories for the Industry dropdown
        ViewBag.Categories = await context.Categories.Where(c => c.IsActive).ToListAsync();

        filter.Matches = await _matchingService.GetFilteredTeamMatchesAsync(
            userId, 
            filter.Role, 
            filter.Skill, 
            filter.IndustryId, 
            filter.Availability, 
            30 // Increased count for directory page
        );

        return View(filter);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Connect(string targetUserId)
    {
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        if (currentUserId == targetUserId) return BadRequest("Cannot connect with yourself.");

        var targetProfile = await HttpContext.RequestServices.GetRequiredService<StartupConnect.Data.ApplicationDbContext>()
            .UserProfiles.Include(p => p.User)
            .FirstOrDefaultAsync(p => p.UserId == targetUserId);
            
        if (targetProfile == null) return NotFound("Target user not found.");

        var currentUser = await HttpContext.RequestServices.GetRequiredService<StartupConnect.Data.ApplicationDbContext>()
            .Users.FindAsync(currentUserId);

        var notificationService = HttpContext.RequestServices.GetRequiredService<StartupConnect.Services.INotificationService>();

        // Send a notification to the target user
        await notificationService.CreateAsync(
            targetUserId, 
            "New Connection Request", 
            $"{currentUser?.FullName ?? "Someone"} wants to connect with you!", 
            $"/Profile/Detail/{currentUserId}"
        );

        return Json(new { success = true, message = "Connection request sent successfully." });
    }
}
