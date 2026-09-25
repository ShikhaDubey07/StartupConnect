using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StartupConnect.Data;
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

    public IdeasController(IIdeaService ideaService, ApplicationDbContext context, IMatchingService matchingService, INotificationService notifications)
    {
        _ideaService = ideaService;
        _context = context;
        _matchingService = matchingService;
        _notifications = notifications;
    }

    public async Task<IActionResult> Browse(IdeaBrowseViewModel filter)
    {
        ViewBag.Categories = await _context.Categories.Where(c => c.IsActive).ToListAsync();
        ViewBag.TotalCount = await _ideaService.GetApprovedIdeasCountAsync(filter);
        ViewBag.TotalPages = (int)Math.Ceiling(ViewBag.TotalCount / (double)filter.PageSize);
        var ideas = await _ideaService.GetApprovedIdeasAsync(filter);
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
            ViewBag.SimilarIdeas = await _matchingService.GetSimilarIdeasAsync(idea);
            ViewBag.ProgressStage = idea.ProgressStage;
            
            // Log view
            if (userId == null || idea.SubmitterUserId != userId)
            {
                _context.IdeaViews.Add(new IdeaView { IdeaId = id, UserId = userId });
                await _context.SaveChangesAsync();
            }
        }

        return View(model);
    }

    [Authorize]
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
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Submit(IdeaSubmitViewModel model)
    {
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

        // Fire and forget AI analysis
        _ = Task.Run(async () =>
        {
            try
            {
                var serviceProvider = HttpContext.RequestServices;
                using var scope = serviceProvider.CreateScope();
                var aiService = scope.ServiceProvider.GetRequiredService<IAIAnalysisService>();
                await aiService.AnalyzeIdeaAsync(ideaId);
            }
            catch (Exception ex)
            {
                // In production, log the exception properly
                Console.WriteLine($"Error running AI analysis: {ex.Message}");
            }
        });

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
    public async Task<IActionResult> RegenerateAnalysis(int id, string? returnUrl = null)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var idea = await _context.Ideas.FirstOrDefaultAsync(i => i.Id == id);
        
        if (idea == null) return NotFound();
        
        // Ensure user is owner or an admin
        if (idea.SubmitterUserId != userId && !User.IsInRole("Admin"))
            return Forbid();

        var aiService = HttpContext.RequestServices.GetRequiredService<IAIAnalysisService>();
        await aiService.AnalyzeIdeaAsync(id);

        TempData["Success"] = "AI Analysis regenerated successfully.";
        
        if (returnUrl == "AiAnalysis")
        {
            return RedirectToAction("AiAnalysis", new { id });
        }
        return RedirectToAction("Detail", new { id });
    }

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> GetHistory(int id)
    {
        var idea = await _context.Ideas.Include(i => i.Category).FirstOrDefaultAsync(i => i.Id == id);
        if (idea == null) return NotFound();

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
        if (idea == null) return NotFound();

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
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var model = await _ideaService.GetIdeaDetailAsync(id, userId);
        
        if (model == null) return NotFound();
        
        // Ensure user is owner or an admin
        if (model.SubmitterName != User.Identity?.Name && !User.IsInRole("Admin")) 
        {
            if (!model.IsOwner && !User.IsInRole("Admin"))
                return Forbid();
        }

        if (model.Analysis == null)
        {
            try
            {
                var aiService = HttpContext.RequestServices.GetRequiredService<IAIAnalysisService>();
                await aiService.AnalyzeIdeaAsync(id);
                // Reload model with new analysis
                model = await _ideaService.GetIdeaDetailAsync(id, userId);
            }
            catch (Exception ex)
            {
                ViewBag.AiError = ex.Message;
            }
        }

        return View(model);
    }

    [Authorize]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateProgress(int id, IdeaProgressStage stage)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var idea = await _context.Ideas.FirstOrDefaultAsync(i => i.Id == id && i.SubmitterUserId == userId);
        if (idea == null) return NotFound();

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
                await _notifications.CreateAsync(idea.SubmitterUserId, "New Like", $"{user?.FullName ?? "Someone"} liked your idea '{idea.Title}'.", $"/Ideas/Detail/{id}");
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
        if (string.IsNullOrWhiteSpace(content)) return BadRequest();
        
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var comment = new IdeaComment
        {
            IdeaId = id,
            UserId = userId,
            Content = content.Trim()
        };
        
        _context.IdeaComments.Add(comment);
        await _context.SaveChangesAsync();
        
        var idea = await _context.Ideas.FindAsync(id);
        if (idea != null && idea.SubmitterUserId != userId)
        {
            var user = await _context.Users.FindAsync(userId);
            await _notifications.CreateAsync(idea.SubmitterUserId, "New Comment", $"{user?.FullName ?? "Someone"} commented on your idea '{idea.Title}'.", $"/Ideas/Detail/{id}");
        }
        
        return RedirectToAction("Detail", new { id = id });
    }

    [Authorize]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ReportSpam(int id, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason)) return BadRequest();
        
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        
        var existingReport = await _context.IdeaReports.FirstOrDefaultAsync(r => r.IdeaId == id && r.UserId == userId);
        if (existingReport == null)
        {
            var report = new IdeaReport
            {
                IdeaId = id,
                UserId = userId,
                Reason = reason.Trim()
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
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        
        // Fetch user's ideas
        var myIdeas = await _context.Ideas
            .Include(i => i.Category)
            .Where(i => i.SubmitterUserId == userId && i.Status == IdeaStatus.Approved)
            .ToListAsync();

        var viewModels = new List<IdeaMatchViewModel>();

        foreach (var idea in myIdeas)
        {
            var matches = await _matchingService.GetSimilarIdeasAsync(idea);
            if (matches.Any())
            {
                var matchDetailsList = new List<MatchedIdeaDetails>();
                foreach (var match in matches)
                {
                    // Reload idea with Submitter and Category to display information
                    var matchedIdea = await _context.Ideas
                        .Include(i => i.Submitter)
                        .Include(i => i.Category)
                        .FirstOrDefaultAsync(i => i.Id == match.Idea.Id);
                        
                    if (matchedIdea == null) continue;

                    var myTokens = GetTokens(idea.Title, idea.Tagline, idea.Description);
                    var theirTokens = GetTokens(matchedIdea.Title, matchedIdea.Tagline, matchedIdea.Description);
                    var commonKeywords = myTokens.Intersect(theirTokens).Where(t => t.Length > 3).Take(5).ToList();

                    matchDetailsList.Add(new MatchedIdeaDetails
                    {
                        Idea = matchedIdea,
                        MatchScore = match.Score,
                        SubmitterName = matchedIdea.Submitter?.FullName ?? "Unknown User",
                        SubmitterId = matchedIdea.SubmitterUserId,
                        CategoryName = matchedIdea.Category?.Name ?? "Unknown",
                        CommonKeywords = commonKeywords,
                        SameCategory = idea.CategoryId == matchedIdea.CategoryId,
                        SameTargetMarket = !string.IsNullOrWhiteSpace(idea.TargetMarket) && idea.TargetMarket.Equals(matchedIdea.TargetMarket, StringComparison.OrdinalIgnoreCase)
                    });
                }
                
                viewModels.Add(new IdeaMatchViewModel
                {
                    MyIdea = idea,
                    Matches = matchDetailsList
                });
            }
        }

        return View(viewModels);
    }

    private HashSet<string> GetTokens(params string[] texts)
    {
        var allText = string.Join(" ", texts).ToLower();
        var chars = allText.Where(c => char.IsLetterOrDigit(c) || char.IsWhiteSpace(c)).ToArray();
        var cleanText = new string(chars);
        return cleanText.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).ToHashSet();
    }

    [Authorize]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Collaborate(int ideaId, int myIdeaId, string message)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var interestService = HttpContext.RequestServices.GetRequiredService<IInterestService>();
        
        var model = new ShowInterestViewModel
        {
            IdeaId = ideaId,
            InterestType = InterestType.Work,
            Message = message
        };
        
        var (success, resultMessage) = await interestService.SubmitInterestAsync(model, userId);
        
        return Json(new { success = success, message = resultMessage });
    }

    [Authorize]
    public async Task<IActionResult> Analytics(int id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        
        var idea = await _context.Ideas
            .Include(i => i.Category)
            .Include(i => i.Views)
            .Include(i => i.Likes)
            .Include(i => i.Comments)
            .Include(i => i.SavedByUsers)
            .Include(i => i.Interests)
            .FirstOrDefaultAsync(i => i.Id == id && i.SubmitterUserId == userId);

        if (idea == null) return NotFound();

        var vm = new IdeaAnalyticsViewModel
        {
            Idea = idea,
            TotalViews = idea.Views.Count,
            TotalLikes = idea.Likes.Count,
            TotalSaves = idea.SavedByUsers.Count,
            TotalComments = idea.Comments.Count,
            TotalInterests = idea.Interests.Count,
            TotalMatches = idea.Interests.Count(i => i.Status == InterestStatus.Accepted)
        };

        // Prepare timeseries data for the last 30 days
        var startDate = DateTime.UtcNow.Date.AddDays(-29);
        var endDate = DateTime.UtcNow.Date;

        for (var date = startDate; date <= endDate; date = date.AddDays(1))
        {
            vm.Dates.Add(date.ToString("MMM dd"));
            vm.ViewsData.Add(idea.Views.Count(v => v.CreatedAt.Date == date));
            vm.LikesData.Add(idea.Likes.Count(l => l.CreatedAt.Date == date));
            vm.SavesData.Add(idea.SavedByUsers.Count(s => s.SavedAt.Date == date));
            vm.CommentsData.Add(idea.Comments.Count(c => c.CreatedAt.Date == date));
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

        switch (type.ToLower())
        {
            case "views":
                var views = await _context.IdeaViews
                    .Include(v => v.User).ThenInclude(u => u!.Profile)
                    .Where(v => v.IdeaId == id)
                    .OrderByDescending(v => v.CreatedAt)
                    .ToListAsync();
                details = views.Select(v => new AnalyticsDetailItemViewModel
                {
                    UserName = v.User?.FullName ?? "Anonymous User",
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
        var ideas = await _context.Ideas
            .Include(i => i.Category)
            .Where(i => i.Status == IdeaStatus.Approved)
            .OrderByDescending(i => (i.Views.Count * 1) + (i.Likes.Count * 5) + (i.Comments.Count * 10))
            .Take(12)
            .Select(i => new IdeaCardViewModel
            {
                Id = i.Id,
                Title = i.Title,
                Tagline = i.Tagline,
                CategoryName = i.Category.Name,
                MinimumFundRequired = i.MinimumFundRequired,
                ExpectedTeamSize = i.ExpectedTeamSize,
                InterestCount = i.Interests.Count,
                LikesCount = i.Likes.Count
            })
            .ToListAsync();
            
        return View(ideas);
    }
}
