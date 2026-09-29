using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using StartupConnect.Data;
using StartupConnect.Infrastructure;
using StartupConnect.Models;
using StartupConnect.Services;
using StartupConnect.ViewModels;
using System.Security.Claims;

namespace StartupConnect.Controllers;

public class IdeasController : Controller
{
    private readonly IIdeaService _ideaService;
    private readonly ApplicationDbContext _context;
    private readonly IMatchingService _matchingService;
    private readonly INotificationService _notifications;
    private readonly IIdeaAnalysisScheduler _analysisScheduler;
    private readonly IAnalysisJobTracker _analysisTracker;
    private readonly IAIAnalysisService _aiService;
    private readonly IConfiguration _configuration;
    private readonly IPrivacyService _privacy;
    private readonly IIdeaViewTracker _viewTracker;

    public const int MaxCommentLength = 1000;
    public const int MaxReportReasonLength = 500;

    public IdeasController(
        IIdeaService ideaService,
        ApplicationDbContext context,
        IMatchingService matchingService,
        INotificationService notifications,
        IIdeaAnalysisScheduler analysisScheduler,
        IAnalysisJobTracker analysisTracker,
        IAIAnalysisService aiService,
        IConfiguration configuration,
        IPrivacyService privacy,
        IIdeaViewTracker viewTracker)
    {
        _privacy = privacy;
        _viewTracker = viewTracker;
        _ideaService = ideaService;
        _context = context;
        _matchingService = matchingService;
        _notifications = notifications;
        _analysisScheduler = analysisScheduler;
        _analysisTracker = analysisTracker;
        _aiService = aiService;
        _configuration = configuration;
    }

    private string? CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier);

    private Task<bool> IsApprovedIdeaAsync(int id) =>
        _context.Ideas.AnyAsync(i => i.Id == id && i.Status == IdeaStatus.Approved);

    /// <summary>Approved ideas are visible to everyone; others only to their owner and admins.</summary>
    private async Task<bool> CanViewIdeaAsync(int id)
    {
        var idea = await _context.Ideas.Where(i => i.Id == id)
            .Select(i => new { i.SubmitterUserId, i.Status })
            .FirstOrDefaultAsync();
        if (idea == null) return false;
        return idea.Status == IdeaStatus.Approved || idea.SubmitterUserId == CurrentUserId || User.IsInRole("Admin");
    }

    private TimeSpan RegenerateCooldown =>
        TimeSpan.FromMinutes(Math.Max(0, _configuration.GetValue<int?>("GoogleGemini:RegenerateCooldownMinutes") ?? 10));

    public async Task<IActionResult> Browse(IdeaBrowseViewModel filter)
    {
        filter.PageSize = Math.Clamp(filter.PageSize, 1, IdeaBrowseViewModel.MaxPageSize);
        if (!IdeaBrowseViewModel.SortOptions.Any(o => o.Value == filter.SortBy)) filter.SortBy = "newest";
        filter.Roles = filter.Roles.Where(r => ProfileViewModel.AvailableSkills.Contains(r)).Distinct().ToList();
        if (filter.MinFund < 0) filter.MinFund = null;
        if (filter.MaxFund < 0) filter.MaxFund = null;
        if (filter.MinFund > filter.MaxFund) (filter.MinFund, filter.MaxFund) = (filter.MaxFund, filter.MinFund);

        var total = await _ideaService.GetApprovedIdeasCountAsync(filter);
        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)filter.PageSize));
        filter.Page = Math.Clamp(filter.Page, 1, totalPages);

        ViewBag.Categories = await _context.Categories.Where(c => c.IsActive).OrderBy(c => c.Name).ToListAsync();
        ViewBag.TotalCount = total;
        ViewBag.TotalPages = totalPages;
        ViewBag.Filter = filter;
        var ideas = await _ideaService.GetApprovedIdeasAsync(filter, CurrentUserId);
        return View(ideas);
    }

    public async Task<IActionResult> Detail(int id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var model = await _ideaService.GetIdeaDetailAsync(id, userId);
        if (model == null) return NotFound();

        var idea = await _context.Ideas.FindAsync(id);
        if (idea != null)
        {
            ViewBag.SimilarIdeas = await _matchingService.GetSimilarIdeasAsync(id, 4);
            ViewBag.ProgressStage = idea.ProgressStage;

            // Public roadmap (read-only) and team for the sidebar.
            ViewBag.Roadmap = new RoadmapViewModel
            {
                Milestones = await _context.IdeaMilestones.AsNoTracking()
                    .Where(m => m.IdeaId == id)
                    .OrderBy(m => m.IsCompleted ? 0 : 1)
                    .ThenBy(m => m.IsCompleted ? m.CompletedAt : m.DueDate)
                    .ThenBy(m => m.CreatedAt)
                    .Select(m => new MilestoneViewModel
                    {
                        Id = m.Id,
                        Title = m.Title,
                        Description = m.Description,
                        DueDate = m.DueDate,
                        IsCompleted = m.IsCompleted,
                        CompletedAt = m.CompletedAt
                    })
                    .ToListAsync()
            };
            var team = await _context.Teams.AsNoTracking()
                .Where(t => t.IdeaId == id)
                .Select(t => new
                {
                    t.Id,
                    t.Status,
                    Members = t.Members.OrderBy(m => m.JoinedAt).Select(m => new WorkspaceMemberViewModel
                    {
                        UserId = m.UserId,
                        FullName = m.User.FullName,
                        Role = m.Role,
                        IsFounder = m.UserId == idea.SubmitterUserId,
                        IsVerified = m.User.Profile != null && m.User.Profile.IsVerifiedFounder,
                        PhotoUrl = m.User.Profile != null ? m.User.Profile.ProfilePhotoUrl : null
                    }).ToList()
                })
                .FirstOrDefaultAsync();
            var members = team?.Members ?? new List<WorkspaceMemberViewModel>();
            ViewBag.TeamMembers = members;

            // Privacy: which profiles this viewer may open, whose names to hide, and the submitter's location.
            var shownUserIds = members.Select(m => m.UserId).Append(idea.SubmitterUserId).ToList();
            var isAdmin = User.IsInRole("Admin");
            var privacy = await _privacy.GetPrivacyAsync(shownUserIds);
            var viewable = await _privacy.GetViewableAsync(shownUserIds, userId, isAdmin);
            ViewBag.ViewableProfiles = viewable;
            ViewBag.HiddenMembers = members.Where(m => privacy[m.UserId].IsPrivate && !viewable.Contains(m.UserId)).Select(m => m.UserId).ToHashSet();
            if (!model.IsOwner && !isAdmin && !privacy[idea.SubmitterUserId].ShowLocation) model.SubmitterCity = null;
            ViewBag.TeamStatus = team?.Status;
            // Founders can always open (or create) the workspace; members get a direct link.
            ViewBag.CanOpenWorkspace = userId != null && (idea.SubmitterUserId == userId || (team?.Members.Any(m => m.UserId == userId) ?? false));
            
            // One view per viewer per 24h; owner and bots are ignored (see IdeaViewTracker).
            await _viewTracker.TrackAsync(HttpContext, id, idea.SubmitterUserId);
        }

        return View(model);
    }

    [Authorize]
    [RequireConfirmedEmail]
    [HttpGet]
    public async Task<IActionResult> Submit(int? id)
    {
        ViewBag.Categories = await _context.Categories.Where(c => c.IsActive).ToListAsync();
        if (id.HasValue)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var existing = await _ideaService.GetIdeaForEditAsync(id.Value, userId);
            if (existing == null) return NotFound();
            return View(existing);
        }
        return View(new IdeaSubmitViewModel());
    }

    [Authorize]
    [RequireConfirmedEmail]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Submit(IdeaSubmitViewModel model)
    {
        if (model.Id.HasValue)
        {
            var ownerId = CurrentUserId;
            if (!await _context.Ideas.AnyAsync(i => i.Id == model.Id.Value && i.SubmitterUserId == ownerId))
                return NotFound();
        }

        if (!model.AcceptTerms)
            ModelState.AddModelError("AcceptTerms", "You must accept the terms.");

        if (!ModelState.IsValid)
        {
            ViewBag.Categories = await _context.Categories.Where(c => c.IsActive).ToListAsync();
            return View(model);
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var profileService = HttpContext.RequestServices.GetRequiredService<IProfileService>();
        if (await profileService.GetCompletionPercentAsync(userId) < 60)
        {
            TempData["Error"] = "Complete at least 60% of your profile before submitting an idea.";
            return RedirectToAction("Profile", "Account");
        }

        var ideaId = await _ideaService.SubmitIdeaAsync(model, userId);

        // AI analysis runs on the background queue (its own DI scope), never on the request.
        // Rapid re-edits don't trigger a new Gemini call while the last analysis is still fresh.
        var lastAnalysis = await _context.IdeaAnalyses.Where(a => a.IdeaId == ideaId)
            .Select(a => (DateTime?)a.GeneratedAt).FirstOrDefaultAsync();
        if (lastAnalysis == null || lastAnalysis.Value + RegenerateCooldown <= DateTime.UtcNow)
            await _analysisScheduler.EnqueueAsync(ideaId);

        TempData["Success"] = "Your idea has been submitted for review!";
        return RedirectToAction("MyIdeas");
    }

    [Authorize]
    public async Task<IActionResult> MyIdeas()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var ideas = await _ideaService.GetUserIdeasAsync(userId);
        ViewBag.IdeaStatuses = await _context.Ideas
            .Where(i => i.SubmitterUserId == userId)
            .ToDictionaryAsync(i => i.Id, i => (i.Status, i.RejectionReason));
        return View(ideas);
    }

    [Authorize]
    [HttpPost, ValidateAntiForgeryToken]
    [EnableRateLimiting(RateLimitPolicies.AiGenerate)]
    public async Task<IActionResult> RegenerateAnalysis(int id, string? returnUrl = null)
    {
        var userId = CurrentUserId;
        var idea = await _context.Ideas
            .Where(i => i.Id == id)
            .Select(i => new { i.Id, i.SubmitterUserId, AnalysisGeneratedAt = (DateTime?)i.Analysis!.GeneratedAt })
            .FirstOrDefaultAsync();

        if (idea == null) return NotFound();

        // Ensure user is owner or an admin
        if (idea.SubmitterUserId != userId && !User.IsInRole("Admin"))
            return Forbid();

        IActionResult Back() => returnUrl == "AiAnalysis"
            ? RedirectToAction(nameof(AiAnalysis), new { id })
            : RedirectToAction(nameof(Detail), new { id });

        if (!_aiService.IsConfigured)
        {
            TempData["Error"] = AiMessages.NotConfigured;
            return Back();
        }

        if (idea.AnalysisGeneratedAt.HasValue)
        {
            var wait = idea.AnalysisGeneratedAt.Value + RegenerateCooldown - DateTime.UtcNow;
            if (wait > TimeSpan.Zero)
            {
                var minutes = Math.Max(1, (int)Math.Ceiling(wait.TotalMinutes));
                TempData["Error"] = $"This analysis was generated recently. You can regenerate it again in {minutes} minute{(minutes == 1 ? "" : "s")}.";
                return Back();
            }
        }

        var result = await _analysisScheduler.EnqueueAsync(id);
        TempData["Success"] = result == AnalysisEnqueueResult.Queued
            ? "Regenerating the AI analysis — this usually takes under a minute."
            : "An AI analysis for this idea is already being generated.";

        return RedirectToAction(nameof(AiAnalysis), new { id });
    }

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> GetHistory(int id)
    {
        var idea = await _context.Ideas.Include(i => i.Category).FirstOrDefaultAsync(i => i.Id == id);
        if (idea == null || !await CanViewIdeaAsync(id)) return NotFound();

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var isOwner = idea.SubmitterUserId == userId || User.IsInRole("Admin");

        var records = await _context.IdeaHistories
            .Include(h => h.Category)
            .Include(h => h.Editor)
            .Where(h => h.IdeaId == id)
            .OrderBy(h => h.CreatedAt)
            .ToListAsync();

        var historyList = new List<IdeaHistoryViewModel>();
        for (int i = 0; i < records.Count; i++)
        {
            var h = records[i];
            // Build change summary by comparing with the NEXT version (or current idea for the last record)
            var changes = new List<string>();
            string nextTitle, nextDesc, nextSolution, nextProblem, nextTarget, nextBusiness, nextCategory;
            decimal nextFund;

            if (i < records.Count - 1)
            {
                var next = records[i + 1];
                nextTitle = next.Title; nextDesc = next.Description; nextSolution = next.Solution;
                nextProblem = next.ProblemStatement; nextTarget = next.TargetMarket;
                nextBusiness = next.BusinessModel; nextCategory = next.Category?.Name ?? "";
                nextFund = next.MinimumFundRequired;
            }
            else
            {
                nextTitle = idea.Title; nextDesc = idea.Description; nextSolution = idea.Solution;
                nextProblem = idea.ProblemStatement; nextTarget = idea.TargetMarket;
                nextBusiness = idea.BusinessModel; nextCategory = idea.Category?.Name ?? "";
                nextFund = idea.MinimumFundRequired;
            }

            if (h.Title != nextTitle) changes.Add("Title");
            if (h.Description != nextDesc) changes.Add("Description");
            if (h.Solution != nextSolution) changes.Add("Solution");
            if (h.ProblemStatement != nextProblem) changes.Add("Problem Statement");
            if (h.TargetMarket != nextTarget) changes.Add("Target Market");
            if (h.BusinessModel != nextBusiness) changes.Add("Business Model");
            if ((h.Category?.Name ?? "") != nextCategory) changes.Add("Category");
            if (h.MinimumFundRequired != nextFund) changes.Add("Fund");

            historyList.Add(new IdeaHistoryViewModel
            {
                Id = h.Id,
                VersionNumber = i + 1,
                Title = h.Title,
                Description = h.Description,
                Solution = h.Solution,
                ProblemStatement = h.ProblemStatement,
                TargetMarket = h.TargetMarket,
                BusinessModel = h.BusinessModel,
                CategoryName = h.Category != null ? h.Category.Name : "Unknown",
                MinimumFundRequired = h.MinimumFundRequired,
                ExpectedTeamSize = h.ExpectedTeamSize,
                EditedAt = h.CreatedAt,
                EditorName = h.Editor != null ? h.Editor.FullName : "Unknown",
                ChangeSummary = changes.Any() ? string.Join(", ", changes) : "No differences"
            });
        }

        // Reverse so newest is first in timeline
        historyList.Reverse();

        ViewBag.IsOwner = isOwner;
        ViewBag.IdeaId = id;
        return PartialView("_IdeaHistoryModal", historyList);
    }

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> CompareVersion(int ideaId, int historyId)
    {
        var idea = await _context.Ideas.Include(i => i.Category).FirstOrDefaultAsync(i => i.Id == ideaId);
        if (idea == null || !await CanViewIdeaAsync(ideaId)) return NotFound();

        var history = await _context.IdeaHistories
            .Include(h => h.Category)
            .Include(h => h.Editor)
            .FirstOrDefaultAsync(h => h.Id == historyId && h.IdeaId == ideaId);
        if (history == null) return NotFound();

        // Count versions to find this one's number
        var versionNumber = await _context.IdeaHistories
            .Where(h => h.IdeaId == ideaId && h.CreatedAt <= history.CreatedAt)
            .CountAsync();

        ViewBag.VersionNumber = versionNumber;
        ViewBag.OldVersion = new IdeaHistoryViewModel
        {
            Id = history.Id,
            VersionNumber = versionNumber,
            Title = history.Title,
            Description = history.Description,
            Solution = history.Solution,
            ProblemStatement = history.ProblemStatement,
            TargetMarket = history.TargetMarket,
            BusinessModel = history.BusinessModel,
            CategoryName = history.Category?.Name ?? "Unknown",
            MinimumFundRequired = history.MinimumFundRequired,
            ExpectedTeamSize = history.ExpectedTeamSize,
            EditedAt = history.CreatedAt,
            EditorName = history.Editor?.FullName ?? "Unknown"
        };

        ViewBag.CurrentVersion = new IdeaHistoryViewModel
        {
            Title = idea.Title,
            Description = idea.Description,
            Solution = idea.Solution,
            ProblemStatement = idea.ProblemStatement,
            TargetMarket = idea.TargetMarket,
            BusinessModel = idea.BusinessModel,
            CategoryName = idea.Category?.Name ?? "Unknown",
            MinimumFundRequired = idea.MinimumFundRequired,
            ExpectedTeamSize = idea.ExpectedTeamSize
        };

        return PartialView("_CompareVersion");
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RestoreVersion(int ideaId, int historyId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var idea = await _context.Ideas.FirstOrDefaultAsync(i => i.Id == ideaId);
        if (idea == null) return NotFound();

        // Only owner or Admin can restore
        if (idea.SubmitterUserId != userId && !User.IsInRole("Admin"))
            return Forbid();

        var history = await _context.IdeaHistories
            .FirstOrDefaultAsync(h => h.Id == historyId && h.IdeaId == ideaId);
        if (history == null) return NotFound();

        // Snapshot the current state before restoring
        var snapshot = new IdeaHistory
        {
            IdeaId = idea.Id,
            Title = idea.Title,
            Tagline = idea.Tagline,
            Description = idea.Description,
            ProblemStatement = idea.ProblemStatement,
            Solution = idea.Solution,
            TargetMarket = idea.TargetMarket,
            BusinessModel = idea.BusinessModel,
            CategoryId = idea.CategoryId,
            MinimumFundRequired = idea.MinimumFundRequired,
            ExpectedTeamSize = idea.ExpectedTeamSize,
            CreatedAt = DateTime.UtcNow,
            EditorId = userId
        };
        _context.IdeaHistories.Add(snapshot);

        // Restore fields from the historical version
        idea.Title = history.Title;
        idea.Tagline = history.Tagline;
        idea.Description = history.Description;
        idea.ProblemStatement = history.ProblemStatement;
        idea.Solution = history.Solution;
        idea.TargetMarket = history.TargetMarket;
        idea.BusinessModel = history.BusinessModel;
        idea.CategoryId = history.CategoryId;
        idea.MinimumFundRequired = history.MinimumFundRequired;
        idea.ExpectedTeamSize = history.ExpectedTeamSize;
        idea.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return Json(new { success = true, message = "Version restored successfully." });
    }

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> AiAnalysis(int id)
    {
        var userId = CurrentUserId!;
        var owner = await _context.Ideas.Where(i => i.Id == id).Select(i => i.SubmitterUserId).FirstOrDefaultAsync();
        if (owner == null) return NotFound();

        // Only the idea's owner or an admin may view (and thereby trigger) the AI analysis.
        var isAdmin = User.IsInRole("Admin");
        if (owner != userId && !isAdmin) return Forbid();

        var model = await _ideaService.GetIdeaDetailAsync(id, userId, includeUnapproved: true);
        if (model == null) return NotFound();

        var job = _analysisTracker.Get(id);
        if (model.Analysis == null && job == null && _aiService.IsConfigured)
        {
            // First visit without an analysis: queue one instead of blocking the request on Gemini.
            await _analysisScheduler.EnqueueAsync(id);
            job = _analysisTracker.Get(id);
        }

        ViewBag.AiConfigured = _aiService.IsConfigured;
        ViewBag.Job = job;
        ViewBag.CanManage = true;
        return View(model);
    }

    /// <summary>Polled by the AI Analysis page while a job is queued/running.</summary>
    [Authorize]
    [HttpGet]
    public async Task<IActionResult> AnalysisStatus(int id)
    {
        var userId = CurrentUserId;
        var idea = await _context.Ideas
            .Where(i => i.Id == id)
            .Select(i => new { i.SubmitterUserId, HasAnalysis = i.Analysis != null })
            .FirstOrDefaultAsync();
        if (idea == null) return NotFound();
        if (idea.SubmitterUserId != userId && !User.IsInRole("Admin")) return Forbid();

        var job = _analysisTracker.Get(id);
        var state = job?.State switch
        {
            AnalysisJobState.Queued => "queued",
            AnalysisJobState.Running => "running",
            AnalysisJobState.Failed => "failed",
            _ => idea.HasAnalysis ? "ready" : "none"
        };
        return Json(new { state, message = job?.Message });
    }

    [Authorize]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateProgress(int id, IdeaProgressStage stage)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var idea = await _context.Ideas.FirstOrDefaultAsync(i => i.Id == id && i.SubmitterUserId == userId);
        if (idea == null) return NotFound();
        if (!Enum.IsDefined(stage)) return BadRequest();

        idea.ProgressStage = stage;
        await _context.SaveChangesAsync();
        TempData["Success"] = "Idea progress updated successfully.";
        return RedirectToAction("Detail", new { id });
    }

    [Authorize]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleLike(int id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        if (!await IsApprovedIdeaAsync(id)) return NotFound();
        var existingLike = await _context.IdeaLikes.FirstOrDefaultAsync(l => l.IdeaId == id && l.UserId == userId);
        
        bool liked = false;
        if (existingLike != null)
        {
            _context.IdeaLikes.Remove(existingLike);
        }
        else
        {
            _context.IdeaLikes.Add(new IdeaLike { IdeaId = id, UserId = userId });
            liked = true;
        }
        
        await _context.SaveChangesAsync();
        
        if (liked)
        {
            var idea = await _context.Ideas.FindAsync(id);
            if (idea != null && idea.SubmitterUserId != userId)
            {
                var user = await _context.Users.FindAsync(userId);
                await _notifications.CreateAsync(idea.SubmitterUserId, "New Like", $"{user?.FullName ?? "Someone"} liked your idea '{idea.Title}'.", $"/Ideas/Detail/{id}", category: NotificationCategory.Like);
            }
        }
        var likesCount = await _context.IdeaLikes.CountAsync(l => l.IdeaId == id);
        
        return Json(new { liked = liked, count = likesCount });
    }

    [Authorize]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleSave(int id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        if (!await IsApprovedIdeaAsync(id)) return NotFound();
        var saved = await _ideaService.ToggleSaveIdeaAsync(id, userId);
        return Json(new { saved = saved });
    }

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> Saved()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var ideas = await _ideaService.GetSavedIdeasAsync(userId);
        return View(ideas);
    }

    [Authorize]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AddComment(int id, string content)
    {
        if (!await IsApprovedIdeaAsync(id)) return NotFound();

        content = content?.Trim() ?? string.Empty;
        if (content.Length == 0)
        {
            TempData["Error"] = "Your comment is empty.";
            return RedirectToAction("Detail", new { id });
        }
        if (content.Length > MaxCommentLength)
        {
            TempData["Error"] = $"Comments can be at most {MaxCommentLength} characters.";
            return RedirectToAction("Detail", new { id });
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var comment = new IdeaComment
        {
            IdeaId = id,
            UserId = userId,
            Content = content
        };
        
        _context.IdeaComments.Add(comment);
        await _context.SaveChangesAsync();
        
        var idea = await _context.Ideas.FindAsync(id);
        if (idea != null && idea.SubmitterUserId != userId)
        {
            var user = await _context.Users.FindAsync(userId);
            await _notifications.CreateAsync(idea.SubmitterUserId, "New Comment", $"{user?.FullName ?? "Someone"} commented on your idea '{idea.Title}'.", $"/Ideas/Detail/{id}", category: NotificationCategory.Comment);
        }
        
        return RedirectToAction("Detail", new { id = id });
    }

    [Authorize]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ReportSpam(int id, string reason)
    {
        var idea = await _context.Ideas.Where(i => i.Id == id && i.Status == IdeaStatus.Approved)
            .Select(i => new { i.SubmitterUserId }).FirstOrDefaultAsync();
        if (idea == null) return NotFound();

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        reason = reason?.Trim() ?? string.Empty;
        if (reason.Length == 0 || reason.Length > MaxReportReasonLength)
        {
            TempData["Error"] = "Please choose a reason for your report.";
            return RedirectToAction("Detail", new { id });
        }
        if (idea.SubmitterUserId == userId)
        {
            TempData["Error"] = "You can't report your own idea.";
            return RedirectToAction("Detail", new { id });
        }
        
        // One open report per user per idea; after moderators handle it the user may report again.
        var existingReport = await _context.IdeaReports.FirstOrDefaultAsync(r => r.IdeaId == id && r.UserId == userId && r.Status == ReportStatus.Open);
        if (existingReport == null)
        {
            var report = new IdeaReport
            {
                IdeaId = id,
                UserId = userId,
                Reason = reason
            };
            
            _context.IdeaReports.Add(report);
            await _context.SaveChangesAsync();
        }
        
        TempData["Success"] = "Thank you for reporting. Our admins will review this idea.";
        return RedirectToAction("Detail", new { id = id });
    }

    [Authorize]
    public async Task<IActionResult> Matches()
    {
        var userId = CurrentUserId!;
        var myIdeas = await _context.Ideas.AsNoTracking()
            .Where(i => i.SubmitterUserId == userId && i.Status == IdeaStatus.Approved)
            .OrderByDescending(i => i.PublishedAt)
            .Select(i => new IdeaMatchViewModel { IdeaId = i.Id, Title = i.Title, CategoryName = i.Category.Name, TargetMarket = i.TargetMarket })
            .ToListAsync();

        // One candidate pool + one display query for all of my ideas (no per-idea / per-match queries).
        var similar = await _matchingService.GetSimilarIdeasForAsync(myIdeas.Select(i => i.IdeaId).ToList(), 6, excludeOwnerId: userId);
        foreach (var idea in myIdeas)
            idea.Matches = similar.TryGetValue(idea.IdeaId, out var list) ? list : new();

        var submitterIds = myIdeas.SelectMany(i => i.Matches).Select(m => m.SubmitterId).Distinct().ToList();
        ViewBag.ViewableProfiles = await _privacy.GetViewableAsync(submitterIds, userId, User.IsInRole("Admin"));
        ViewBag.HasApprovedIdeas = myIdeas.Count > 0;
        ViewBag.PendingIdeas = await _context.Ideas.CountAsync(i => i.SubmitterUserId == userId
            && (i.Status == IdeaStatus.Submitted || i.Status == IdeaStatus.UnderReview));
        return View(myIdeas);
    }

    [Authorize]
    [RequireConfirmedEmail]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Collaborate(int ideaId, int myIdeaId, string message)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var interestService = HttpContext.RequestServices.GetRequiredService<IInterestService>();
        
        var model = new ShowInterestViewModel
        {
            IdeaId = ideaId,
            InterestType = InterestType.Work,
            Message = message?.Length > 500 ? message[..500] : message
        };
        
        var (success, resultMessage) = await interestService.SubmitInterestAsync(model, userId);
        
        return Json(new { success = success, message = resultMessage });
    }

    [Authorize]
    public async Task<IActionResult> Analytics(int id)
    {
        var userId = CurrentUserId!;
        var idea = await _context.Ideas.AsNoTracking()
            .Include(i => i.Category)
            .FirstOrDefaultAsync(i => i.Id == id && i.SubmitterUserId == userId);
        if (idea == null) return NotFound();

        // All counts are SQL-side; views are unique per viewer per 24h (see IdeaViewTracker).
        var vm = new IdeaAnalyticsViewModel
        {
            Idea = idea,
            TotalViews = await _context.IdeaViews.CountAsync(v => v.IdeaId == id),
            UniqueViewers = await _context.IdeaViews.Where(v => v.IdeaId == id).Select(v => v.ViewerKey ?? ("legacy:" + v.Id)).Distinct().CountAsync(),
            TotalLikes = await _context.IdeaLikes.CountAsync(l => l.IdeaId == id),
            TotalSaves = await _context.SavedIdeas.CountAsync(s => s.IdeaId == id),
            TotalComments = await _context.IdeaComments.CountAsync(c => c.IdeaId == id),
            TotalInterests = await _context.Interests.CountAsync(i => i.IdeaId == id),
            TotalMatches = await _context.Interests.CountAsync(i => i.IdeaId == id && i.Status == InterestStatus.Accepted)
        };

        // Time series for the last 30 days, grouped by day in SQL.
        var startDate = DateTime.UtcNow.Date.AddDays(-29);
        var endDate = DateTime.UtcNow.Date;
        var views = await _context.IdeaViews.Where(v => v.IdeaId == id && v.CreatedAt >= startDate)
            .GroupBy(v => v.CreatedAt.Date).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count);
        var likes = await _context.IdeaLikes.Where(l => l.IdeaId == id && l.CreatedAt >= startDate)
            .GroupBy(l => l.CreatedAt.Date).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count);
        var saves = await _context.SavedIdeas.Where(s => s.IdeaId == id && s.SavedAt >= startDate)
            .GroupBy(s => s.SavedAt.Date).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count);
        var comments = await _context.IdeaComments.Where(c => c.IdeaId == id && c.CreatedAt >= startDate)
            .GroupBy(c => c.CreatedAt.Date).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count);

        for (var date = startDate; date <= endDate; date = date.AddDays(1))
        {
            vm.Dates.Add(date.ToString("MMM dd"));
            vm.ViewsData.Add(views.GetValueOrDefault(date));
            vm.LikesData.Add(likes.GetValueOrDefault(date));
            vm.SavesData.Add(saves.GetValueOrDefault(date));
            vm.CommentsData.Add(comments.GetValueOrDefault(date));
        }

        return View(vm);
    }

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> AnalyticsDetails(int id, string type)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        
        // Verify ownership
        var idea = await _context.Ideas.FirstOrDefaultAsync(i => i.Id == id && i.SubmitterUserId == userId);
        if (idea == null) return NotFound();

        var details = new List<AnalyticsDetailItemViewModel>();

        switch ((type ?? string.Empty).ToLowerInvariant())
        {
            case "views":
                var views = await _context.IdeaViews
                    .Include(v => v.User).ThenInclude(u => u!.Profile)
                    .Where(v => v.IdeaId == id)
                    .OrderByDescending(v => v.CreatedAt)
                    .Take(200)
                    .ToListAsync();
                details = views.Select(v => new AnalyticsDetailItemViewModel
                {
                    UserName = v.User?.FullName ?? "Anonymous visitor",
                    UserProfilePhoto = v.User?.Profile?.ProfilePhotoUrl,
                    ActivityType = "View",
                    ActivityDate = v.CreatedAt
                }).ToList();
                break;
                
            case "likes":
                var likes = await _context.IdeaLikes
                    .Include(l => l.User).ThenInclude(u => u.Profile)
                    .Where(l => l.IdeaId == id)
                    .OrderByDescending(l => l.CreatedAt)
                    .ToListAsync();
                details = likes.Select(l => new AnalyticsDetailItemViewModel
                {
                    UserName = l.User?.FullName ?? "Unknown User",
                    UserProfilePhoto = l.User?.Profile?.ProfilePhotoUrl,
                    ActivityType = "Like",
                    ActivityDate = l.CreatedAt
                }).ToList();
                break;
                
            case "saves":
                var saves = await _context.SavedIdeas
                    .Include(s => s.User).ThenInclude(u => u.Profile)
                    .Where(s => s.IdeaId == id)
                    .OrderByDescending(s => s.SavedAt)
                    .ToListAsync();
                details = saves.Select(s => new AnalyticsDetailItemViewModel
                {
                    UserName = s.User?.FullName ?? "Unknown User",
                    UserProfilePhoto = s.User?.Profile?.ProfilePhotoUrl,
                    ActivityType = "Save",
                    ActivityDate = s.SavedAt
                }).ToList();
                break;
                
            case "comments":
                var comments = await _context.IdeaComments
                    .Include(c => c.User).ThenInclude(u => u.Profile)
                    .Where(c => c.IdeaId == id)
                    .OrderByDescending(c => c.CreatedAt)
                    .ToListAsync();
                details = comments.Select(c => new AnalyticsDetailItemViewModel
                {
                    UserName = c.User?.FullName ?? "Unknown User",
                    UserProfilePhoto = c.User?.Profile?.ProfilePhotoUrl,
                    ActivityType = "Comment",
                    ActivityDate = c.CreatedAt,
                    ExtraDetails = c.Content
                }).ToList();
                break;
                
            case "requests":
                var requests = await _context.Interests
                    .Include(i => i.User).ThenInclude(u => u.Profile)
                    .Where(i => i.IdeaId == id)
                    .OrderByDescending(i => i.CreatedAt)
                    .ToListAsync();
                details = requests.Select(i => new AnalyticsDetailItemViewModel
                {
                    UserName = i.User?.FullName ?? "Unknown User",
                    UserProfilePhoto = i.User?.Profile?.ProfilePhotoUrl,
                    ActivityType = $"Collaboration Request ({i.InterestType}) - {i.Status}",
                    ActivityDate = i.CreatedAt,
                    ExtraDetails = i.Message ?? (i.ProposedInvestmentAmount.HasValue ? $"Proposed Investment: ₹{i.ProposedInvestmentAmount:N0}" : "No message provided.")
                }).ToList();
                break;
                
            case "matches":
                var matches = await _context.Interests
                    .Include(i => i.User).ThenInclude(u => u.Profile)
                    .Where(i => i.IdeaId == id && i.Status == InterestStatus.Accepted)
                    .OrderByDescending(i => i.CreatedAt)
                    .ToListAsync();
                details = matches.Select(i => new AnalyticsDetailItemViewModel
                {
                    UserName = i.User?.FullName ?? "Unknown User",
                    UserProfilePhoto = i.User?.Profile?.ProfilePhotoUrl,
                    ActivityType = $"Match ({i.InterestType})",
                    ActivityDate = i.CreatedAt,
                    ExtraDetails = i.Message
                }).ToList();
                break;
        }

        return Json(details);
    }

    [Authorize]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleSaveIdea(int id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        if (!await IsApprovedIdeaAsync(id)) return NotFound();
        var savedIdea = await _context.SavedIdeas.FirstOrDefaultAsync(s => s.IdeaId == id && s.UserId == userId);
        
        if (savedIdea != null)
        {
            _context.SavedIdeas.Remove(savedIdea);
        }
        else
        {
            _context.SavedIdeas.Add(new SavedIdea { IdeaId = id, UserId = userId });
        }
        
        await _context.SaveChangesAsync();
        return RedirectToAction("Detail", new { id });
    }

    public async Task<IActionResult> Trending()
    {
        var ideas = await _ideaService.GetTrendingIdeasAsync(12, CurrentUserId);
        return View(ideas);
    }
}
