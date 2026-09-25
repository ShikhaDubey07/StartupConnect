using Microsoft.EntityFrameworkCore;
using StartupConnect.Data;
using StartupConnect.Models;
using StartupConnect.ViewModels;

namespace StartupConnect.Services;

public interface IIdeaService
{
    Task<List<IdeaCardViewModel>> GetApprovedIdeasAsync(IdeaBrowseViewModel filter);
    Task<int> GetApprovedIdeasCountAsync(IdeaBrowseViewModel filter);
    Task<IdeaDetailViewModel?> GetIdeaDetailAsync(int id, string? currentUserId);
    Task<int> SubmitIdeaAsync(IdeaSubmitViewModel model, string userId);
    Task<List<IdeaCardViewModel>> GetUserIdeasAsync(string userId);
    Task<IdeaSubmitViewModel?> GetIdeaForEditAsync(int id, string userId);
    Task ApproveIdeaAsync(int id, string adminId);
    Task RejectIdeaAsync(int id, string reason, string adminId);
    Task<List<Idea>> GetPendingIdeasAsync();
    Task<List<IdeaCardViewModel>> GetMostLikedIdeasThisWeekAsync(int count);
    Task<bool> ToggleSaveIdeaAsync(int ideaId, string userId);
    Task<List<IdeaCardViewModel>> GetSavedIdeasAsync(string userId);
}

public class IdeaService : IIdeaService
{
    private readonly ApplicationDbContext _context;
    private readonly INotificationService _notifications;
    private readonly IMatchingService _matchingService;

    public IdeaService(ApplicationDbContext context, INotificationService notifications, IMatchingService matchingService)
    {
        _context = context;
        _notifications = notifications;
        _matchingService = matchingService;
    }

    public async Task<List<IdeaCardViewModel>> GetApprovedIdeasAsync(IdeaBrowseViewModel filter)
    {
        var query = BuildApprovedQuery(filter);
        query = ApplySort(query, filter.SortBy);

        var ideas = await query
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync();
            
        // We don't have current user ID here, so IsSavedByCurrentUser defaults to false.
        // It's usually fine for browse if we don't need accurate save state on cards for anonymous,
        // but if we need it for authenticated, we should pass currentUserId.
        // For now, MapToCard just maps it.
        return ideas.Select(i => MapToCard(i, null)).ToList();
    }

    public async Task<int> GetApprovedIdeasCountAsync(IdeaBrowseViewModel filter)
    {
        return await BuildApprovedQuery(filter).CountAsync();
    }

    public async Task<IdeaDetailViewModel?> GetIdeaDetailAsync(int id, string? currentUserId)
    {
        var idea = await _context.Ideas
            .Include(i => i.Category)
            .Include(i => i.Submitter)
            .Include(i => i.RolesNeeded)
            .Include(i => i.Interests)
            .Include(i => i.Likes)
            .Include(i => i.SavedByUsers)
            .Include(i => i.Comments)
                .ThenInclude(c => c.User)
            .Include(i => i.History)
                .ThenInclude(h => h.Category)
            .Include(i => i.Analysis)
            .FirstOrDefaultAsync(i => i.Id == id && i.Status == IdeaStatus.Approved);

        if (idea == null) return null;

        return new IdeaDetailViewModel
        {
            Id = idea.Id,
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
            SubmitterCity = idea.Submitter.City,
            InterestCount = idea.Interests.Count,
            TotalPledged = idea.Interests.Where(x => x.ProposedInvestmentAmount.HasValue).Sum(x => x.ProposedInvestmentAmount ?? 0),
            CanShowInterest = currentUserId != null && idea.SubmitterUserId != currentUserId,
            IsOwner = currentUserId == idea.SubmitterUserId,
            IsLikedByCurrentUser = currentUserId != null && idea.Likes.Any(l => l.UserId == currentUserId),
            IsSavedByCurrentUser = currentUserId != null && idea.SavedByUsers.Any(s => s.UserId == currentUserId),
            LikesCount = idea.Likes.Count,
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

        await _context.SaveChangesAsync();
        return idea.Id;
    }

    public async Task<List<IdeaCardViewModel>> GetUserIdeasAsync(string userId)
    {
        var ideas = await _context.Ideas
            .Include(i => i.Category)
            .Include(i => i.Interests)
            .Include(i => i.RolesNeeded)
            .Include(i => i.Likes)
            .Include(i => i.SavedByUsers)
            .Where(i => i.SubmitterUserId == userId)
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync();
            
        return ideas.Select(i => MapToCard(i, userId)).ToList();
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

        await _context.SaveChangesAsync();
        
        var matches = await _matchingService.GetSimilarIdeasAsync(idea);
        if (matches.Any())
        {
            await _notifications.CreateAsync(idea.SubmitterUserId, "Idea Approved & Matches Found! 🎉",
                $"Your idea '{idea.Title}' is live, and we found {matches.Count} potential matches for you!", $"/Ideas/Matches");
                
            foreach (var match in matches)
            {
                await _notifications.CreateAsync(match.Idea.SubmitterUserId, "New Match Found",
                    $"A newly approved idea '{idea.Title}' matches your idea '{match.Idea.Title}'.", $"/Ideas/Detail/{idea.Id}");
            }
        }
        else
        {
            await _notifications.CreateAsync(idea.SubmitterUserId, "Idea Approved! 🎉",
                $"Your idea '{idea.Title}' has been approved and is now live.", $"/Ideas/Detail/{id}");
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
            $"Your idea '{idea.Title}' was not approved. Reason: {reason}", "/Ideas/MyIdeas");
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
        var query = _context.Ideas
            .Include(i => i.Category)
            .Include(i => i.Interests)
            .Include(i => i.RolesNeeded)
            .Include(i => i.Likes)
            .Include(i => i.Analysis)  // needed for AI Score sort and badge
            .Where(i => i.Status == IdeaStatus.Approved);

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var term = filter.Search.Trim();
            query = query.Where(i => i.Title.Contains(term) || i.Tagline.Contains(term) || i.Description.Contains(term));
        }

        if (filter.CategoryId.HasValue)
            query = query.Where(i => i.CategoryId == filter.CategoryId);

        if (filter.MinFund.HasValue)
            query = query.Where(i => i.MinimumFundRequired >= filter.MinFund);

        if (filter.MaxFund.HasValue)
            query = query.Where(i => i.MinimumFundRequired <= filter.MaxFund);

        return query;
    }

    private static IQueryable<Idea> ApplySort(IQueryable<Idea> query, string sortBy) => sortBy switch
    {
        "popular"  => query.OrderByDescending(i => i.Interests.Count),
        "fund-low" => query.OrderBy(i => i.MinimumFundRequired),
        "ai-score" => query.OrderByDescending(i => i.Analysis != null ? i.Analysis.OverallScore : 0),
        _          => query.OrderByDescending(i => i.PublishedAt)
    };

    private static IdeaCardViewModel MapToCard(Idea i, string? currentUserId) => new()
    {
        Id = i.Id,
        Title = i.Title,
        Tagline = i.Tagline,
        CategoryName = i.Category.Name,
        CategoryIcon = i.Category.IconClass,
        MinimumFundRequired = i.MinimumFundRequired,
        ExpectedTeamSize = i.ExpectedTeamSize,
        InterestCount = i.Interests.Count,
        LikesCount = i.Likes?.Count ?? 0,
        IsSavedByCurrentUser = currentUserId != null && (i.SavedByUsers?.Any(s => s.UserId == currentUserId) ?? false),
        RolesNeeded = i.RolesNeeded.Select(r => r.RoleName).ToList(),
        PublishedAt = i.PublishedAt,
        AiScore = i.Analysis?.OverallScore
    };

    public async Task<List<IdeaCardViewModel>> GetMostLikedIdeasThisWeekAsync(int count)
    {
        var oneWeekAgo = DateTime.UtcNow.AddDays(-7);
        var ideas = await _context.Ideas
            .Include(i => i.Category)
            .Include(i => i.Interests)
            .Include(i => i.RolesNeeded)
            .Include(i => i.Likes)
            .Include(i => i.SavedByUsers)
            .Where(i => i.Status == IdeaStatus.Approved && i.Likes.Any(l => l.CreatedAt >= oneWeekAgo))
            .OrderByDescending(i => i.Likes.Count(l => l.CreatedAt >= oneWeekAgo))
            .Take(count)
            .ToListAsync();
            
        return ideas.Select(i => MapToCard(i, null)).ToList();
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
        var savedIdeas = await _context.SavedIdeas
            .Include(s => s.Idea)
                .ThenInclude(i => i.Category)
            .Include(s => s.Idea)
                .ThenInclude(i => i.Interests)
            .Include(s => s.Idea)
                .ThenInclude(i => i.RolesNeeded)
            .Include(s => s.Idea)
                .ThenInclude(i => i.Likes)
            .Include(s => s.Idea)
                .ThenInclude(i => i.SavedByUsers)
            .Where(s => s.UserId == userId && s.Idea.Status == IdeaStatus.Approved)
            .OrderByDescending(s => s.SavedAt)
            .Select(s => s.Idea)
            .ToListAsync();
            
        return savedIdeas.Select(i => MapToCard(i, userId)).ToList();
    }
}
