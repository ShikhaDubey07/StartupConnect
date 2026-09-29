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
    private readonly IConnectionService _connections;

    public MatchesController(IMatchingService matchingService, IAIAnalysisService aiService, IConnectionService connections)
    {
        _matchingService = matchingService;
        _aiService = aiService;
        _connections = connections;
    }

    public async Task<IActionResult> Index()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

        var results    = await _matchingService.GetCoFounderMatchesAsync(userId);
        var coFounders = results.Matches;
        var investors  = await _matchingService.GetRecommendedInvestorsAsync(userId);
        var ideas      = await _matchingService.GetRecommendedIdeasForInvestmentAsync(userId);

        ViewBag.CoFounderResults = results;
        ViewBag.CoFounders    = coFounders;
        ViewBag.Investors     = investors;
        ViewBag.IdeasToInvest = ideas;
        ViewBag.ConnectionStates = await _connections.GetStatesAsync(userId,
            coFounders.Select(m => m.UserId).Concat(investors.Select(i => i.UserId)));

        // --- AI: co-founder match rationales (top 5, cached per pair) ---
        var rationaleDict = new Dictionary<string, string>();
        // Skip Gemini entirely when no API key is configured (no pointless exceptions per request).
        var rationaleTop5 = _aiService.IsConfigured ? coFounders.Take(5).ToList() : new();
        if (rationaleTop5.Any())
        {
            var rationaleTasks = rationaleTop5.Select(async m =>
            {
                try
                {
                    var r = await _aiService.GetCoFounderRationaleAsync(userId, m.UserId);
                    return (m.UserId, Rationale: r ?? string.Empty);
                }
                catch
                {
                    return (m.UserId, Rationale: string.Empty);
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
        var pitchTop5 = _aiService.IsConfigured ? ideas.Take(5).ToList() : new();
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

        ViewBag.Categories = await context.Categories.Where(c => c.IsActive).OrderBy(c => c.Name).ToListAsync();

        filter.Results = await _matchingService.FindTeamAsync(userId, new StartupConnect.Services.Matching.TeamSearch
        {
            Role = filter.Role,
            Skill = string.IsNullOrWhiteSpace(filter.Skill) ? null : filter.Skill.Trim()[..Math.Min(filter.Skill.Trim().Length, 60)],
            IndustryId = filter.IndustryId,
            Availability = filter.Availability,
            ForIdeaId = filter.ForIdeaId,
            IncludeConnections = filter.IncludeConnections
        }, 30);
        // An idea id that isn't one of the viewer's live ideas silently falls back to the general directory.
        if (filter.ForIdeaId.HasValue && filter.Results.ForIdea == null) filter.ForIdeaId = null;

        ViewBag.ConnectionStates = await _connections.GetStatesAsync(userId, filter.Results.Matches.Select(m => m.UserId));
        return View(filter);
    }
}
