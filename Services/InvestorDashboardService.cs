using Microsoft.EntityFrameworkCore;
using StartupConnect.Data;
using StartupConnect.Models;
using StartupConnect.ViewModels;

namespace StartupConnect.Services;

public interface IInvestorDashboardService
{
    Task<InvestorDashboardViewModel> BuildAsync(string userId, InvestorDashboardFilter filter, int take = 24);
}

/// <summary>
/// Ranks approved ideas for an investor (0–100):
/// category match 40 · ticket fits the remaining ask 25 · engagement 20 (relative to the strongest candidate) · recency 15.
/// Every point awarded comes with a human-readable reason shown on the card.
/// </summary>
public sealed class InvestorDashboardService : IInvestorDashboardService
{
    private readonly ApplicationDbContext _db;

    public InvestorDashboardService(ApplicationDbContext db) => _db = db;

    public static string CapacityLabel(InvestmentCapacity c) => c switch
    {
        InvestmentCapacity.UpTo10K => "Up to ₹10K",
        InvestmentCapacity.From10KTo50K => "₹10K – ₹50K",
        InvestmentCapacity.From50KTo1L => "₹50K – ₹1L",
        InvestmentCapacity.Above1L => "Above ₹1L",
        _ => "Not set"
    };

    /// <summary>Largest cheque for a capacity band (null = no upper limit).</summary>
    public static decimal? TicketMax(InvestmentCapacity c) => c switch
    {
        InvestmentCapacity.UpTo10K => 10_000m,
        InvestmentCapacity.From10KTo50K => 50_000m,
        InvestmentCapacity.From50KTo1L => 100_000m,
        InvestmentCapacity.Above1L => null,
        _ => 0m
    };

    public static string Rupees(decimal amount) => amount switch
    {
        >= 10_000_000m => $"₹{amount / 10_000_000m:0.##} Cr",
        >= 100_000m => $"₹{amount / 100_000m:0.##} L",
        >= 1_000m => $"₹{amount / 1_000m:0.#}K",
        _ => $"₹{amount:0}"
    };

    public async Task<InvestorDashboardViewModel> BuildAsync(string userId, InvestorDashboardFilter filter, int take = 24)
    {
        var profile = await _db.UserProfiles.AsNoTracking()
            .Include(p => p.InterestTags).ThenInclude(t => t.Category)
            .FirstOrDefaultAsync(p => p.UserId == userId);

        var vm = new InvestorDashboardViewModel
        {
            IsInvestor = profile?.IsInvestor == true,
            Capacity = profile?.InvestmentCapacity ?? InvestmentCapacity.None,
            InterestCategories = profile?.InterestTags.Select(t => t.Category).OrderBy(c => c.Name).ToList() ?? new(),
            Filter = filter,
            AllCategories = await _db.Categories.AsNoTracking().Where(c => c.IsActive).OrderBy(c => c.Name).ToListAsync()
        };

        await LoadInterestsAndSavedAsync(vm, userId);
        if (!vm.IsInvestor) return vm;

        var myCategoryIds = vm.InterestCategories.Select(c => c.Id).ToHashSet();

        var query = _db.Ideas.AsNoTracking().Where(i => i.Status == IdeaStatus.Approved && i.SubmitterUserId != userId);
        if (filter.CategoryId.HasValue) query = query.Where(i => i.CategoryId == filter.CategoryId.Value);
        if (filter.MyCategories && myCategoryIds.Count > 0) query = query.Where(i => myCategoryIds.Contains(i.CategoryId));
        if (filter.Stage.HasValue) query = query.Where(i => i.ProgressStage == filter.Stage.Value);
        if (filter.MinFund.HasValue) query = query.Where(i => i.MinimumFundRequired >= filter.MinFund.Value);
        if (filter.MaxFund.HasValue) query = query.Where(i => i.MinimumFundRequired <= filter.MaxFund.Value);

        var candidates = await query
            .Select(i => new
            {
                i.Id, i.Title, i.Tagline, i.CategoryId, CategoryName = i.Category.Name, CategoryIcon = i.Category.IconClass,
                i.ProgressStage, i.MinimumFundRequired, i.PublishedAt, i.CreatedAt,
                FounderName = i.Submitter.FullName,
                FounderVerified = i.Submitter.Profile != null && i.Submitter.Profile.IsVerifiedFounder,
                Views = i.Views.Count(),
                Likes = i.Likes.Count(),
                Comments = i.Comments.Count(),
                InterestCount = i.Interests.Count(),
                Committed = i.Interests.Where(x => x.Status == InterestStatus.Accepted && x.ProposedInvestmentAmount != null)
                    .Sum(x => (decimal?)x.ProposedInvestmentAmount) ?? 0m,
                IsSaved = i.SavedByUsers.Any(s => s.UserId == userId),
                MyInterest = i.Interests.Where(x => x.UserId == userId).Select(x => (InterestStatus?)x.Status).FirstOrDefault()
            })
            .ToListAsync();
        vm.CandidateCount = candidates.Count;

        double Raw(int views, int likes, int comments, int interests) => views + likes * 5 + comments * 10 + interests * 8;
        var maxRaw = candidates.Count == 0 ? 0 : candidates.Max(c => Raw(c.Views, c.Likes, c.Comments, c.InterestCount));
        var ticket = TicketMax(vm.Capacity);
        var now = DateTime.UtcNow;

        foreach (var c in candidates)
        {
            var match = new InvestorIdeaMatch
            {
                IdeaId = c.Id, Title = c.Title, Tagline = c.Tagline, CategoryName = c.CategoryName, CategoryIcon = c.CategoryIcon,
                Stage = c.ProgressStage, FundingAsk = c.MinimumFundRequired, Committed = c.Committed,
                FounderName = c.FounderName, FounderVerified = c.FounderVerified, PublishedAt = c.PublishedAt,
                Likes = c.Likes, Comments = c.Comments, Views = c.Views, IsSaved = c.IsSaved, MyInterestStatus = c.MyInterest
            };
            double score = 0;

            // 1. Category (40)
            if (myCategoryIds.Contains(c.CategoryId))
            {
                score += 40;
                match.Reasons.Add($"In {c.CategoryName}, one of your focus areas");
            }

            // 2. Ticket fit (25)
            var remaining = match.Remaining;
            if (vm.Capacity != InvestmentCapacity.None)
            {
                if (remaining <= 0)
                {
                    score += 5;
                    match.Reasons.Add("Already fully committed — follow-on interest only");
                }
                else if (ticket == null)
                {
                    score += 25;
                    match.Reasons.Add($"Your capacity ({CapacityLabel(vm.Capacity)}) can anchor the {Rupees(remaining)} still needed");
                }
                else
                {
                    var share = (double)(ticket.Value / remaining);
                    if (share >= 1)
                    {
                        score += 25;
                        match.Reasons.Add($"Your ticket (up to {Rupees(ticket.Value)}) could close the remaining {Rupees(remaining)}");
                    }
                    else if (share >= 0.1)
                    {
                        score += 18;
                        match.Reasons.Add($"Your ticket could cover ~{Math.Round(share * 100)}% of the {Rupees(remaining)} still needed");
                    }
                    else
                    {
                        score += 8;
                        match.Reasons.Add($"A smaller cheque here: ~{Math.Max(1, Math.Round(share * 100))}% of the {Rupees(remaining)} needed");
                    }
                }
            }

            // 3. Engagement (20, relative to the most engaged candidate)
            var raw = Raw(c.Views, c.Likes, c.Comments, c.InterestCount);
            if (maxRaw > 0 && raw > 0)
            {
                var points = raw / maxRaw * 20;
                score += points;
                if (points >= 10)
                    match.Reasons.Add($"Strong traction: {c.Likes} like{(c.Likes == 1 ? "" : "s")}, {c.Comments} comment{(c.Comments == 1 ? "" : "s")}, {c.InterestCount} interested");
            }

            // 4. Recency (15)
            var ageDays = (now - (c.PublishedAt ?? c.CreatedAt)).TotalDays;
            if (ageDays <= 7) { score += 15; match.Reasons.Add("Published this week"); }
            else if (ageDays <= 30) { score += 10; match.Reasons.Add("Published in the last month"); }
            else if (ageDays <= 90) score += 5;

            match.Score = (int)Math.Round(Math.Min(100, score));
            vm.Matches.Add(match);
        }

        vm.Matches = vm.Matches
            .OrderByDescending(m => m.Score)
            .ThenByDescending(m => m.PublishedAt)
            .Take(take)
            .ToList();
        return vm;
    }

    private async Task LoadInterestsAndSavedAsync(InvestorDashboardViewModel vm, string userId)
    {
        vm.Interests = await _db.Interests.AsNoTracking()
            .Where(i => i.UserId == userId && (i.InterestType == InterestType.Invest || i.InterestType == InterestType.Both))
            .OrderByDescending(i => i.CreatedAt)
            .Select(i => new InvestorInterestRow
            {
                InterestId = i.Id,
                IdeaId = i.IdeaId,
                IdeaTitle = i.Idea.Title,
                IdeaIsLive = i.Idea.Status == IdeaStatus.Approved,
                InterestType = i.InterestType,
                Amount = i.ProposedInvestmentAmount,
                Status = i.Status,
                CreatedAt = i.CreatedAt
            })
            .ToListAsync();
        vm.TotalProposed = vm.Interests.Where(i => i.Status is InterestStatus.Pending or InterestStatus.Accepted).Sum(i => i.Amount ?? 0);
        vm.AcceptedTotal = vm.Interests.Where(i => i.Status == InterestStatus.Accepted).Sum(i => i.Amount ?? 0);
        vm.PendingInterests = vm.Interests.Count(i => i.Status == InterestStatus.Pending);

        vm.SavedIdeas = await _db.SavedIdeas.AsNoTracking()
            .Where(s => s.UserId == userId && s.Idea.Status == IdeaStatus.Approved)
            .OrderByDescending(s => s.SavedAt)
            .Select(s => new InvestorSavedIdea
            {
                IdeaId = s.IdeaId,
                Title = s.Idea.Title,
                CategoryName = s.Idea.Category.Name,
                FundingAsk = s.Idea.MinimumFundRequired,
                SavedAt = s.SavedAt
            })
            .Take(8)
            .ToListAsync();
    }
}
