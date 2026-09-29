using Microsoft.EntityFrameworkCore;
using StartupConnect.Data;
using StartupConnect.Models;
using StartupConnect.ViewModels;

namespace StartupConnect.Services;

public interface IIdeaService
{
    /// <summary>One page of approved ideas matching the filter (sorted in SQL; counts are SQL-side projections).</summary>
    Task<List<IdeaCardViewModel>> GetApprovedIdeasAsync(IdeaBrowseViewModel filter, string? currentUserId = null);
    Task<int> GetApprovedIdeasCountAsync(IdeaBrowseViewModel filter);
    /// <summary>Approved ideas ranked by time-decayed engagement (views, likes, comments, interests).</summary>
    Task<List<IdeaCardViewModel>> GetTrendingIdeasAsync(int count, string? currentUserId = null);
    /// <summary>
    /// Returns an approved idea's details. Pass includeUnapproved only after checking the caller is
    /// the owner or an admin.
    /// </summary>
    Task<IdeaDetailViewModel?> GetIdeaDetailAsync(int id, string? currentUserId, bool includeUnapproved = false);
    Task<int> SubmitIdeaAsync(IdeaSubmitViewModel model, string userId);
    Task<List<IdeaCardViewModel>> GetUserIdeasAsync(string userId);
    Task<IdeaSubmitViewModel?> GetIdeaForEditAsync(int id, string userId);
    Task ApproveIdeaAsync(int id, string adminId);
    Task RejectIdeaAsync(int id, string reason, string adminId);
    Task<List<Idea>> GetPendingIdeasAsync();
    Task<List<IdeaCardViewModel>> GetMostLikedIdeasThisWeekAsync(int count, string? currentUserId = null);
    Task<bool> ToggleSaveIdeaAsync(int ideaId, string userId);
    Task<List<IdeaCardViewModel>> GetSavedIdeasAsync(string userId);
}

public class IdeaService : IIdeaService
{
    private readonly ApplicationDbContext _context;
    private readonly INotificationService _notifications;
    private readonly IMatchingService _matchingService;
    private readonly IActivityService _activity;

    public IdeaService(ApplicationDbContext context, INotificationService notifications, IMatchingService matchingService, IActivityService activity)
    {
        _context = context;
        _notifications = notifications;
        _matchingService = matchingService;
        _activity = activity;
    }

    public async Task<List<IdeaCardViewModel>> GetApprovedIdeasAsync(IdeaBrowseViewModel filter, string? currentUserId = null)
    {
        var pageSize = Math.Clamp(filter.PageSize, 1, IdeaBrowseViewModel.MaxPageSize);
        var page = Math.Max(1, filter.Page);
        var query = ApplySort(BuildApprovedQuery(filter), filter.SortBy, DateTime.UtcNow);
        return await ProjectCards(query.Skip((page - 1) * pageSize).Take(pageSize), currentUserId).ToListAsync();
    }

    public Task<int> GetApprovedIdeasCountAsync(IdeaBrowseViewModel filter) => BuildApprovedQuery(filter).CountAsync();

    public async Task<List<IdeaCardViewModel>> GetTrendingIdeasAsync(int count, string? currentUserId = null)
    {
        var query = OrderByTrending(_context.Ideas.Where(i => i.Status == IdeaStatus.Approved), DateTime.UtcNow);
        return await ProjectCards(query.Take(count), currentUserId).ToListAsync();
    }

    public async Task<IdeaDetailViewModel?> GetIdeaDetailAsync(int id, string? currentUserId, bool includeUnapproved = false)
    {
        var idea = await _context.Ideas
            .Include(i => i.Category)
            .Include(i => i.Submitter).ThenInclude(u => u.Profile)
            .Include(i => i.RolesNeeded)
            .Include(i => i.Comments)
                .ThenInclude(c => c.User)
            .Include(i => i.History)
                .ThenInclude(h => h.Category)
            .Include(i => i.Analysis)
            .AsSplitQuery()
            .FirstOrDefaultAsync(i => i.Id == id && (includeUnapproved || i.Status == IdeaStatus.Approved));

        if (idea == null) return null;

        // Engagement numbers are SQL-side aggregates (no likes/interests/saves collections loaded).
        var stats = await _context.Ideas.AsNoTracking().Where(i => i.Id == id).Select(i => new
        {
            Interests = i.Interests.Count(x => x.Status != InterestStatus.Cancelled),
            Pledged = i.Interests.Where(x => x.Status != InterestStatus.Cancelled && x.Status != InterestStatus.Rejected)
                .Sum(x => (decimal?)x.ProposedInvestmentAmount) ?? 0m,
            Likes = i.Likes.Count(),
            Liked = currentUserId != null && i.Likes.Any(l => l.UserId == currentUserId),
            Saved = currentUserId != null && i.SavedByUsers.Any(s => s.UserId == currentUserId)
        }).FirstAsync();

        return new IdeaDetailViewModel
        {
            Id = idea.Id,
            Status = idea.Status,
            Title = idea.Title,
            Tagline = idea.Tagline,
            Description = idea.Description,
            PreviousVersion = idea.History.OrderByDescending(h => h.CreatedAt).Select(h => new IdeaHistoryViewModel
            {
                Title = h.Title,
                Description = h.Description,
                Solution = h.Solution,
                ProblemStatement = h.ProblemStatement,
                TargetMarket = h.TargetMarket,
                BusinessModel = h.BusinessModel,
                CategoryName = h.Category.Name,
                MinimumFundRequired = h.MinimumFundRequired,
                ExpectedTeamSize = h.ExpectedTeamSize,
                EditedAt = h.CreatedAt
            }).FirstOrDefault(),
            ProblemStatement = idea.ProblemStatement,
            Solution = idea.Solution,
            TargetMarket = idea.TargetMarket,
            BusinessModel = idea.BusinessModel,
            CategoryName = idea.Category.Name,
            MinimumFundRequired = idea.MinimumFundRequired,
            ExpectedTeamSize = idea.ExpectedTeamSize,
            RolesNeeded = idea.RolesNeeded.Select(r => r.RoleName).ToList(),
            SubmitterName = idea.Submitter.FullName,
            SubmitterId = idea.SubmitterUserId,
            SubmitterVerified = idea.Submitter.Profile?.IsVerifiedFounder == true,
            SubmitterCity = idea.Submitter.City,
            InterestCount = stats.Interests,
            TotalPledged = stats.Pledged,
            CanShowInterest = currentUserId != null && idea.SubmitterUserId != currentUserId && idea.Status == IdeaStatus.Approved,
            IsOwner = currentUserId == idea.SubmitterUserId,
            IsLikedByCurrentUser = stats.Liked,
            IsSavedByCurrentUser = stats.Saved,
            LikesCount = stats.Likes,
            PublishedAt = idea.PublishedAt,
            Comments = idea.Comments.OrderByDescending(c => c.CreatedAt).Select(c => new IdeaCommentViewModel
            {
                Id = c.Id,
                UserName = c.User.FullName,
                Content = c.Content,
                CreatedAt = c.CreatedAt
            }).ToList(),
            Analysis = idea.Analysis != null ? new IdeaAnalysisViewModel
            {
                Summary = idea.Analysis.Summary,
                ProblemsAndSolutions = idea.Analysis.ProblemsAndSolutions,
                TargetUsers = idea.Analysis.TargetUsers,
                MarketPotential = idea.Analysis.MarketPotential,
                Risks = idea.Analysis.Risks,
                RevenueModels = idea.Analysis.RevenueModels,
                TeamSuggestions = idea.Analysis.TeamSuggestions,
                OverallScore = idea.Analysis.OverallScore,
                SimilarIdeas = string.IsNullOrEmpty(idea.Analysis.SimilarIdeasJson) ? new List<SimilarIdeaViewModel>() : System.Text.Json.JsonSerializer.Deserialize<List<SimilarIdeaViewModel>>(idea.Analysis.SimilarIdeasJson) ?? new List<SimilarIdeaViewModel>(),
                GeneratedAt = idea.Analysis.GeneratedAt
            } : null
        };
    }

    public async Task<int> SubmitIdeaAsync(IdeaSubmitViewModel model, string userId)
    {
        if (model.CategoryId == 0 && !string.IsNullOrWhiteSpace(model.CustomCategory))
        {
            var existingCat = await _context.Categories.FirstOrDefaultAsync(c => c.Name.ToLower() == model.CustomCategory.ToLower());
            if (existingCat != null)
            {
                model.CategoryId = existingCat.Id;
            }
            else
            {
                var newCat = new Category { Name = model.CustomCategory, IsActive = true, IconClass = "bi-lightbulb" };
                _context.Categories.Add(newCat);
                await _context.SaveChangesAsync();
                model.CategoryId = newCat.Id;
            }
        }

        Idea idea;
        if (model.Id.HasValue)
        {
            idea = await _context.Ideas
                .Include(i => i.RolesNeeded)
                .FirstAsync(i => i.Id == model.Id && i.SubmitterUserId == userId);

            _context.IdeaRolesNeeded.RemoveRange(idea.RolesNeeded);
        }
        else
        {
            idea = new Idea { SubmitterUserId = userId };
            _context.Ideas.Add(idea);
        }

        if (model.Id.HasValue)
        {
            var history = new IdeaHistory
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
            _context.IdeaHistories.Add(history);
        }

        idea.Title = model.Title;
        idea.Tagline = model.Tagline;
        idea.Description = model.Description;
        idea.ProblemStatement = model.ProblemStatement;
        idea.Solution = model.Solution;
        idea.TargetMarket = model.TargetMarket;
        idea.BusinessModel = model.BusinessModel;
        idea.CategoryId = model.CategoryId.GetValueOrDefault();
        idea.MinimumFundRequired = model.MinimumFundRequired;
        idea.ExpectedTeamSize = model.ExpectedTeamSize;
        if (idea.Status != IdeaStatus.Approved)
        {
            idea.Status = IdeaStatus.Submitted;
        }
        idea.UpdatedAt = DateTime.UtcNow;
        idea.RolesNeeded = model.RolesNeeded.Select(r => new IdeaRoleNeeded { RoleName = r }).ToList();

        var isNew = !model.Id.HasValue;
        await _context.SaveChangesAsync();
        if (isNew)
        {
            await _activity.RecordAsync(userId, ActivityTypes.IdeaSubmitted,
                $"You submitted \"{idea.Title}\" for review.", "/Ideas/MyIdeas");
        }
        return idea.Id;
    }

    public async Task<List<IdeaCardViewModel>> GetUserIdeasAsync(string userId)
    {
        var query = _context.Ideas.Where(i => i.SubmitterUserId == userId).OrderByDescending(i => i.CreatedAt);
        return await ProjectCards(query, userId).ToListAsync();
    }

    public async Task<IdeaSubmitViewModel?> GetIdeaForEditAsync(int id, string userId)
    {
        var idea = await _context.Ideas
            .Include(i => i.RolesNeeded)
            .FirstOrDefaultAsync(i => i.Id == id && i.SubmitterUserId == userId);

        if (idea == null) return null;

        return new IdeaSubmitViewModel
        {
            Id = idea.Id,
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
            RolesNeeded = idea.RolesNeeded.Select(r => r.RoleName).ToList()
        };
    }

    public async Task ApproveIdeaAsync(int id, string adminId)
    {
        var idea = await _context.Ideas.FindAsync(id)
            ?? throw new InvalidOperationException("Idea not found");

        idea.Status = IdeaStatus.Approved;
        idea.PublishedAt = DateTime.UtcNow;
        idea.UpdatedAt = DateTime.UtcNow;

        _context.ReviewNotes.Add(new ReviewNote
        {
            IdeaId = id,
            AdminUserId = adminId,
            Note = "Approved and published"
        });
        await _activity.RecordAsync(idea.SubmitterUserId, ActivityTypes.IdeaApproved,
            $"\"{idea.Title}\" was approved and published.", $"/Ideas/Detail/{id}", save: false);

        await _context.SaveChangesAsync();
        
        // Similar ideas by other founders; each of those founders hears about it once (strong matches only).
        var matches = await _matchingService.GetSimilarIdeasAsync(idea.Id, 5, excludeOwnerId: idea.SubmitterUserId);
        if (matches.Any())
        {
            await _notifications.CreateAsync(idea.SubmitterUserId, "Idea Approved & Matches Found! 🎉",
                $"Your idea '{idea.Title}' is live, and we found {matches.Count} similar idea{(matches.Count == 1 ? "" : "s")} by other founders.", "/Ideas/Matches", category: NotificationCategory.Moderation);

            foreach (var match in matches.Where(m => m.Score >= 30).GroupBy(m => m.SubmitterId).Select(g => g.First()))
            {
                await _notifications.CreateAsync(match.SubmitterId, "New Match Found",
                    $"A newly approved idea '{idea.Title}' is similar to your idea '{match.Title}'.", $"/Ideas/Detail/{idea.Id}", category: NotificationCategory.Match);
            }
        }
        else
        {
            await _notifications.CreateAsync(idea.SubmitterUserId, "Idea Approved! 🎉",
                $"Your idea '{idea.Title}' has been approved and is now live.", $"/Ideas/Detail/{id}", category: NotificationCategory.Moderation);
        }
    }

    public async Task RejectIdeaAsync(int id, string reason, string adminId)
    {
        var idea = await _context.Ideas.FindAsync(id)
            ?? throw new InvalidOperationException("Idea not found");

        idea.Status = IdeaStatus.Rejected;
        idea.RejectionReason = reason;
        idea.UpdatedAt = DateTime.UtcNow;

        _context.ReviewNotes.Add(new ReviewNote
        {
            IdeaId = id,
            AdminUserId = adminId,
            Note = reason
        });

        await _context.SaveChangesAsync();
        await _notifications.CreateAsync(idea.SubmitterUserId, "Idea Needs Revision",
            $"Your idea '{idea.Title}' was not approved. Reason: {reason}", "/Ideas/MyIdeas", category: NotificationCategory.Moderation);
    }

    public async Task<List<Idea>> GetPendingIdeasAsync()
    {
        return await _context.Ideas
            .Include(i => i.Category)
            .Include(i => i.Submitter)
            .Include(i => i.RolesNeeded)
            .Where(i => i.Status == IdeaStatus.Submitted || i.Status == IdeaStatus.UnderReview)
            .OrderBy(i => i.CreatedAt)
            .ToListAsync();
    }

    private IQueryable<Idea> BuildApprovedQuery(IdeaBrowseViewModel filter)
    {
        var query = _context.Ideas.AsNoTracking().Where(i => i.Status == IdeaStatus.Approved);

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var term = filter.Search.Trim();
            if (term.Length > 100) term = term[..100];
            query = query.Where(i => i.Title.Contains(term) || i.Tagline.Contains(term) || i.Description.Contains(term)
                || i.ProblemStatement.Contains(term) || i.Solution.Contains(term));
        }

        if (filter.CategoryId.HasValue)
            query = query.Where(i => i.CategoryId == filter.CategoryId);

        if (filter.Stage.HasValue)
            query = query.Where(i => i.ProgressStage == filter.Stage.Value);

        var (min, max) = (filter.MinFund, filter.MaxFund);
        if (min.HasValue && max.HasValue && min > max) (min, max) = (max, min);
        if (min.HasValue) query = query.Where(i => i.MinimumFundRequired >= min);
        if (max.HasValue) query = query.Where(i => i.MinimumFundRequired <= max);

        var roles = filter.Roles.Where(r => !string.IsNullOrWhiteSpace(r)).Select(r => r.Trim()).Distinct().Take(20).ToList();
        if (roles.Count > 0)
            query = query.Where(i => i.RolesNeeded.Any(r => roles.Contains(r.RoleName)));

        if (filter.VerifiedOnly)
            query = query.Where(i => i.Submitter.Profile != null && i.Submitter.Profile.IsVerifiedFounder);

        return query;
    }

    private static IQueryable<Idea> ApplySort(IQueryable<Idea> query, string? sortBy, DateTime now) => sortBy switch
    {
        "trending"  => OrderByTrending(query, now),
        "liked"     => query.OrderByDescending(i => i.Likes.Count()).ThenByDescending(i => i.PublishedAt).ThenByDescending(i => i.Id),
        "viewed"    => query.OrderByDescending(i => i.Views.Count()).ThenByDescending(i => i.PublishedAt).ThenByDescending(i => i.Id),
        "interest" or "popular" => query.OrderByDescending(i => i.Interests.Count(x => x.Status != InterestStatus.Cancelled)).ThenByDescending(i => i.PublishedAt).ThenByDescending(i => i.Id),
        "fund-asc" or "fund-low" => query.OrderBy(i => i.MinimumFundRequired).ThenByDescending(i => i.Id),
        "fund-desc" => query.OrderByDescending(i => i.MinimumFundRequired).ThenByDescending(i => i.Id),
        "ai-score"  => query.OrderByDescending(i => i.Analysis != null ? i.Analysis.OverallScore : -1).ThenByDescending(i => i.PublishedAt).ThenByDescending(i => i.Id),
        _           => query.OrderByDescending(i => i.PublishedAt).ThenByDescending(i => i.Id)
    };

    /// <summary>
    /// Time-decayed engagement, computed in SQL. Each event (unique view 1, like 4, comment 6, interest 8) counts
    /// 1.75× in its first 3 days, 0.75× in days 3–7, 0.25× in days 7–30 and not at all afterwards, so fresh activity wins.
    /// </summary>
    private static IQueryable<Idea> OrderByTrending(IQueryable<Idea> query, DateTime now)
    {
        var d3 = now.AddDays(-3);
        var d7 = now.AddDays(-7);
        var d30 = now.AddDays(-30);
        return query
            .OrderByDescending(i =>
                1.0 * (i.Views.Count(v => v.CreatedAt >= d3) + 4 * i.Likes.Count(l => l.CreatedAt >= d3)
                       + 6 * i.Comments.Count(c => c.CreatedAt >= d3) + 8 * i.Interests.Count(x => x.CreatedAt >= d3))
                + 0.5 * (i.Views.Count(v => v.CreatedAt >= d7) + 4 * i.Likes.Count(l => l.CreatedAt >= d7)
                       + 6 * i.Comments.Count(c => c.CreatedAt >= d7) + 8 * i.Interests.Count(x => x.CreatedAt >= d7))
                + 0.25 * (i.Views.Count(v => v.CreatedAt >= d30) + 4 * i.Likes.Count(l => l.CreatedAt >= d30)
                       + 6 * i.Comments.Count(c => c.CreatedAt >= d30) + 8 * i.Interests.Count(x => x.CreatedAt >= d30)))
            .ThenByDescending(i => i.PublishedAt)
            .ThenByDescending(i => i.Id);
    }

    /// <summary>Card projection: every count is a SQL sub-query, no collections are loaded.</summary>
    private static IQueryable<IdeaCardViewModel> ProjectCards(IQueryable<Idea> query, string? currentUserId) =>
        query.Select(i => new IdeaCardViewModel
        {
            Id = i.Id,
            Title = i.Title,
            Tagline = i.Tagline,
            CategoryName = i.Category.Name,
            CategoryIcon = i.Category.IconClass,
            MinimumFundRequired = i.MinimumFundRequired,
            ExpectedTeamSize = i.ExpectedTeamSize,
            InterestCount = i.Interests.Count(x => x.Status != InterestStatus.Cancelled),
            LikesCount = i.Likes.Count(),
            ViewsCount = i.Views.Count(),
            IsSavedByCurrentUser = currentUserId != null && i.SavedByUsers.Any(s => s.UserId == currentUserId),
            RolesNeeded = i.RolesNeeded.Select(r => r.RoleName).ToList(),
            PublishedAt = i.PublishedAt,
            AiScore = i.Analysis != null ? (int?)i.Analysis.OverallScore : null,
            SubmitterVerified = i.Submitter.Profile != null && i.Submitter.Profile.IsVerifiedFounder,
            ProgressStage = i.ProgressStage
        });

    public async Task<List<IdeaCardViewModel>> GetMostLikedIdeasThisWeekAsync(int count, string? currentUserId = null)
    {
        var oneWeekAgo = DateTime.UtcNow.AddDays(-7);
        var query = _context.Ideas.AsNoTracking()
            .Where(i => i.Status == IdeaStatus.Approved && i.Likes.Any(l => l.CreatedAt >= oneWeekAgo))
            .OrderByDescending(i => i.Likes.Count(l => l.CreatedAt >= oneWeekAgo))
            .ThenByDescending(i => i.Likes.Count())
            .ThenByDescending(i => i.PublishedAt)
            .Take(count);
        return await ProjectCards(query, currentUserId).ToListAsync();
    }

    public async Task<bool> ToggleSaveIdeaAsync(int ideaId, string userId)
    {
        var existingSave = await _context.SavedIdeas.FirstOrDefaultAsync(s => s.IdeaId == ideaId && s.UserId == userId);
        
        bool isSaved = false;
        if (existingSave != null)
        {
            _context.SavedIdeas.Remove(existingSave);
        }
        else
        {
            _context.SavedIdeas.Add(new SavedIdea { IdeaId = ideaId, UserId = userId });
            isSaved = true;
        }
        
        await _context.SaveChangesAsync();
        return isSaved;
    }

    public async Task<List<IdeaCardViewModel>> GetSavedIdeasAsync(string userId)
    {
        var query = _context.SavedIdeas
            .Where(s => s.UserId == userId && s.Idea.Status == IdeaStatus.Approved)
            .OrderByDescending(s => s.SavedAt)
            .Select(s => s.Idea);
        return await ProjectCards(query, userId).ToListAsync();
    }
}
