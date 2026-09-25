using Microsoft.EntityFrameworkCore;
using StartupConnect.Data;
using StartupConnect.Models;

namespace StartupConnect.Services;

public interface IMatchingService
{
    Task<List<(Idea Idea, double Score)>> GetSimilarIdeasAsync(Idea currentIdea, int count = 5);
    Task<List<(UserProfile Profile, double Score)>> GetCoFounderMatchesAsync(string userId, int count = 10);
    Task<List<(UserProfile Profile, double Score)>> GetFilteredTeamMatchesAsync(string userId, string? role, string? skill, int? industryId, TimeAvailability? availability, int count = 20);
    Task<List<UserProfile>> GetRecommendedInvestorsAsync(string userId, int count = 10);
    Task<List<Idea>> GetRecommendedIdeasForInvestmentAsync(string userId, int count = 10);
}

public class MatchingService : IMatchingService
{
    private readonly ApplicationDbContext _context;

    public MatchingService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<(Idea Idea, double Score)>> GetSimilarIdeasAsync(Idea currentIdea, int count = 5)
    {
        var otherIdeas = await _context.Ideas
            .Where(i => i.Id != currentIdea.Id && i.Status == IdeaStatus.Approved)
            .ToListAsync();

        var currentTokens = GetTokens(currentIdea.Title, currentIdea.Tagline, currentIdea.Description);
        if (!currentTokens.Any())
            return new List<(Idea, double)>();

        var results = new List<(Idea Idea, double Score)>();
        foreach (var idea in otherIdeas)
        {
            var tokens = GetTokens(idea.Title, idea.Tagline, idea.Description);
            if (!tokens.Any()) continue;

            var intersection = currentTokens.Intersect(tokens).Count();
            var union = currentTokens.Union(tokens).Count();
            var score = (double)intersection / union;

            if (score > 0)
                results.Add((idea, score * 100));
        }

        return results.OrderByDescending(r => r.Score).Take(count).ToList();
    }

    public async Task<List<(UserProfile Profile, double Score)>> GetCoFounderMatchesAsync(string userId, int count = 10)
    {
        var currentUserProfile = await _context.UserProfiles
            .Include(p => p.User)
            .Include(p => p.Skills)
            .Include(p => p.InterestTags)
                .ThenInclude(t => t.Category)
            .FirstOrDefaultAsync(p => p.UserId == userId);

        if (currentUserProfile == null) return new List<(UserProfile, double)>();

        var otherProfiles = await _context.UserProfiles
            .Include(p => p.User)
            .Include(p => p.Skills)
            .Include(p => p.InterestTags)
                .ThenInclude(t => t.Category)
            .Where(p => p.UserId != userId && p.User.IsActive)
            .ToListAsync();

        var results = new List<(UserProfile Profile, double Score)>();

        foreach (var profile in otherProfiles)
        {
            double maxScore = 100.0; // We'll normalize to 100
            double currentScore = 0;

            // 1. Availability Matching (up to 25 points)
            if (profile.TimeAvailability == currentUserProfile.TimeAvailability) 
                currentScore += 15;
            
            // Compare hours per week (up to 10 points for closeness)
            int hourDiff = Math.Abs(profile.HoursPerWeek - currentUserProfile.HoursPerWeek);
            if (hourDiff <= 5) currentScore += 10;
            else if (hourDiff <= 15) currentScore += 5;

            // 2. Skills Matching (up to 25 points)
            var commonSkills = profile.Skills.Select(s => s.SkillName.ToLower())
                .Intersect(currentUserProfile.Skills.Select(s => s.SkillName.ToLower())).Count();
            currentScore += Math.Min(commonSkills * 10, 25);

            // 3. Interests / Industry Matching (up to 25 points)
            var commonInterests = profile.InterestTags.Select(t => t.Category.Name.ToLower())
                .Intersect(currentUserProfile.InterestTags.Select(t => t.Category.Name.ToLower())).Count();
            currentScore += Math.Min(commonInterests * 10, 25);

            // 4. Location Matching (up to 15 points)
            if (!string.IsNullOrWhiteSpace(profile.User.City) && 
                profile.User.City.Equals(currentUserProfile.User.City, StringComparison.OrdinalIgnoreCase))
            {
                currentScore += 10;
            }
            if (!string.IsNullOrWhiteSpace(profile.User.State) && 
                profile.User.State.Equals(currentUserProfile.User.State, StringComparison.OrdinalIgnoreCase))
            {
                currentScore += 5;
            }

            // 5. Role Complementarity / Requirements (up to 10 points)
            // Example: If one is investor and other is founder, they might be complementary
            if (profile.IsInvestor != currentUserProfile.IsInvestor)
            {
                currentScore += 10;
            }
            else
            {
                // If both are founders, maybe complementary skills give bonus
                if (commonSkills > 0 && commonInterests > 0)
                {
                    currentScore += 5; 
                }
            }

            // Normalize match percentage
            double matchPercent = Math.Min((currentScore / maxScore) * 100, 99.0); // Cap at 99%
            // Ensure minimum of 10% if they have anything in common
            if (currentScore > 0) matchPercent = Math.Max(matchPercent, 10.0);

            if (matchPercent >= 10.0) // Only show if at least 10% match
                results.Add((profile, Math.Round(matchPercent)));
        }

        return results.OrderByDescending(r => r.Score).Take(count).ToList();
    }

    public async Task<List<(UserProfile Profile, double Score)>> GetFilteredTeamMatchesAsync(string userId, string? role, string? skill, int? industryId, TimeAvailability? availability, int count = 20)
    {
        var currentUserProfile = await _context.UserProfiles
            .Include(p => p.Skills)
            .Include(p => p.InterestTags)
                .ThenInclude(t => t.Category)
            .Include(p => p.User)
            .FirstOrDefaultAsync(p => p.UserId == userId);

        if (currentUserProfile == null) return new List<(UserProfile, double)>();

        var query = _context.UserProfiles
            .Include(p => p.User)
            .Include(p => p.Skills)
            .Include(p => p.InterestTags)
                .ThenInclude(t => t.Category)
            .Where(p => p.UserId != userId && p.User.IsActive)
            .AsQueryable();

        if (!string.IsNullOrEmpty(role) && role != "Any")
        {
            bool isInvestorFilter = role == "Investor";
            query = query.Where(p => p.IsInvestor == isInvestorFilter);
        }

        if (availability.HasValue)
        {
            query = query.Where(p => p.TimeAvailability == availability.Value);
        }

        if (industryId.HasValue)
        {
            query = query.Where(p => p.InterestTags.Any(t => t.CategoryId == industryId.Value));
        }

        var otherProfiles = await query.ToListAsync();

        // In-memory filter for skills since it's a bit complex for EF with string Contains
        if (!string.IsNullOrEmpty(skill))
        {
            var skillLower = skill.ToLower();
            otherProfiles = otherProfiles.Where(p => p.Skills.Any(s => s.SkillName.ToLower().Contains(skillLower))).ToList();
        }

        var results = new List<(UserProfile Profile, double Score)>();

        foreach (var profile in otherProfiles)
        {
            double maxScore = 100.0;
            double currentScore = 0;

            if (profile.TimeAvailability == currentUserProfile.TimeAvailability) currentScore += 15;
            
            int hourDiff = Math.Abs(profile.HoursPerWeek - currentUserProfile.HoursPerWeek);
            if (hourDiff <= 5) currentScore += 10;
            else if (hourDiff <= 15) currentScore += 5;

            var commonSkills = profile.Skills.Select(s => s.SkillName.ToLower())
                .Intersect(currentUserProfile.Skills.Select(s => s.SkillName.ToLower())).Count();
            currentScore += Math.Min(commonSkills * 10, 25);

            var commonInterests = profile.InterestTags.Select(t => t.Category.Name.ToLower())
                .Intersect(currentUserProfile.InterestTags.Select(t => t.Category.Name.ToLower())).Count();
            currentScore += Math.Min(commonInterests * 10, 25);

            if (!string.IsNullOrWhiteSpace(profile.User.City) && 
                profile.User.City.Equals(currentUserProfile.User.City, StringComparison.OrdinalIgnoreCase))
                currentScore += 10;
            
            if (!string.IsNullOrWhiteSpace(profile.User.State) && 
                profile.User.State.Equals(currentUserProfile.User.State, StringComparison.OrdinalIgnoreCase))
                currentScore += 5;

            if (profile.IsInvestor != currentUserProfile.IsInvestor)
                currentScore += 10;
            else if (commonSkills > 0 && commonInterests > 0)
                currentScore += 5;

            double matchPercent = Math.Min((currentScore / maxScore) * 100, 99.0);
            if (currentScore > 0) matchPercent = Math.Max(matchPercent, 10.0);
            
            // For the specific Find Team page, we show results even if match is low, 
            // as they matched the explicit filters, but we still rank by match percent.
            results.Add((profile, Math.Round(matchPercent)));
        }

        return results.OrderByDescending(r => r.Score).Take(count).ToList();
    }

    public async Task<List<UserProfile>> GetRecommendedInvestorsAsync(string userId, int count = 10)
    {
        var maxFundingNeeded = await _context.Ideas
            .Where(i => i.SubmitterUserId == userId && i.Status == IdeaStatus.Approved)
            .MaxAsync(i => (decimal?)i.MinimumFundRequired) ?? 0m;

        if (maxFundingNeeded == 0) return new List<UserProfile>();

        var requiredCapacity = GetCapacityForAmount(maxFundingNeeded);

        return await _context.UserProfiles
            .Include(p => p.User)
            .Where(p => p.UserId != userId && p.IsInvestor && p.InvestmentCapacity >= requiredCapacity)
            .Take(count)
            .ToListAsync();
    }

    public async Task<List<Idea>> GetRecommendedIdeasForInvestmentAsync(string userId, int count = 10)
    {
        var profile = await _context.UserProfiles.FirstOrDefaultAsync(p => p.UserId == userId);
        if (profile == null || !profile.IsInvestor || profile.InvestmentCapacity == InvestmentCapacity.None)
            return new List<Idea>();

        var maxAmount = GetMaxAmountForCapacity(profile.InvestmentCapacity);

        return await _context.Ideas
            .Include(i => i.Submitter)
            .Where(i => i.Status == IdeaStatus.Approved && i.MinimumFundRequired <= maxAmount && i.MinimumFundRequired > 0)
            .Take(count)
            .ToListAsync();
    }

    private HashSet<string> GetTokens(params string[] texts)
    {
        var allText = string.Join(" ", texts).ToLower();
        var chars = allText.Where(c => char.IsLetterOrDigit(c) || char.IsWhiteSpace(c)).ToArray();
        var cleanText = new string(chars);
        return cleanText.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).ToHashSet();
    }

    private InvestmentCapacity GetCapacityForAmount(decimal amount)
    {
        if (amount <= 10000) return InvestmentCapacity.UpTo10K;
        if (amount <= 50000) return InvestmentCapacity.From10KTo50K;
        if (amount <= 100000) return InvestmentCapacity.From50KTo1L;
        return InvestmentCapacity.Above1L;
    }

    private decimal GetMaxAmountForCapacity(InvestmentCapacity capacity)
    {
        return capacity switch
        {
            InvestmentCapacity.UpTo10K => 10000m,
            InvestmentCapacity.From10KTo50K => 50000m,
            InvestmentCapacity.From50KTo1L => 100000m,
            InvestmentCapacity.Above1L => decimal.MaxValue,
            _ => 0m
        };
    }
}
