using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using StartupConnect.Data;
using StartupConnect.Models;
using StartupConnect.Services.Matching;

namespace StartupConnect.Services;

public interface IMatchingService
{
    /// <summary>Smart Matches co-founder list: complementary people first, network (connections/teammates) excluded.</summary>
    Task<TeamMatchResults> GetCoFounderMatchesAsync(string userId, int count = 12);
    /// <summary>Find Team directory with filters and optional "for idea" mode.</summary>
    Task<TeamMatchResults> FindTeamAsync(string userId, TeamSearch search, int count = 30);
    /// <summary>Approved ideas similar to <paramref name="ideaId"/>.</summary>
    Task<List<SimilarIdeaMatch>> GetSimilarIdeasAsync(int ideaId, int count = 4, string? excludeOwnerId = null);
    /// <summary>Similar approved ideas for several ideas at once (one candidate pool, no N+1).</summary>
    Task<Dictionary<int, List<SimilarIdeaMatch>>> GetSimilarIdeasForAsync(IReadOnlyCollection<int> ideaIds, int count = 5, string? excludeOwnerId = null);
    Task<List<UserProfile>> GetRecommendedInvestorsAsync(string userId, int count = 10);
    Task<List<Idea>> GetRecommendedIdeasForInvestmentAsync(string userId, int count = 10);
}

public class MatchingService : IMatchingService
{
    /// <summary>Max candidates pulled from the DB (pre-ranked in SQL) before in-memory scoring.</summary>
    public const int CandidateCap = 400;
    /// <summary>Max ideas per similar-idea candidate bucket (same category / other categories).</summary>
    public const int SimilarPoolPerBucket = 150;
    /// <summary>Smart Matches hides very weak matches; Find Team shows everything that passes the filters.</summary>
    public const int MinCoFounderScore = 15;

    private static readonly IdeaStatus[] InactiveIdeaStatuses = { IdeaStatus.Rejected, IdeaStatus.Unpublished };

    private readonly ApplicationDbContext _context;
    private readonly IMemoryCache _cache;

    public MatchingService(ApplicationDbContext context, IMemoryCache cache)
    {
        _context = context;
        _cache = cache;
    }

    // ======================================================================
    // Co-founder / team matching
    // ======================================================================

    public Task<TeamMatchResults> GetCoFounderMatchesAsync(string userId, int count = 12) =>
        MatchAsync(userId, new TeamSearch { Role = "Founder" }, count, MinCoFounderScore);

    public Task<TeamMatchResults> FindTeamAsync(string userId, TeamSearch search, int count = 30) =>
        MatchAsync(userId, search, count, minScore: 0);

    private sealed class CandidateRow
    {
        public string UserId { get; init; } = string.Empty;
        public string FullName { get; init; } = string.Empty;
        public string? PhotoUrl { get; init; }
        public string? Bio { get; init; }
        public string? City { get; init; }
        public string? State { get; init; }
        public bool ShowLocation { get; init; }
        public bool IsInvestor { get; init; }
        public bool IsVerified { get; init; }
        public int Completion { get; init; }
        public TimeAvailability Availability { get; init; }
        public int Hours { get; init; }
        public List<string> Skills { get; init; } = new();
        public List<CategoryRef> Categories { get; init; } = new();
    }

    private sealed class CategoryRef
    {
        public int Id { get; init; }
        public string Name { get; init; } = string.Empty;
    }

    private async Task<TeamMatchResults> MatchAsync(string userId, TeamSearch search, int count, int minScore)
    {
        var me = await _context.UserProfiles.AsNoTracking()
            .Where(p => p.UserId == userId)
            .Select(p => new
            {
                p.TimeAvailability,
                p.HoursPerWeek,
                p.User.City,
                p.User.State,
                Skills = p.Skills.Select(s => s.SkillName).ToList(),
                Categories = p.InterestTags.Select(t => new CategoryRef { Id = t.CategoryId, Name = t.Category.Name }).ToList()
            })
            .AsSplitQuery()
            .FirstOrDefaultAsync();

        var myIdeas = await LoadSeekerIdeasAsync(userId);
        if (me == null)
            return new TeamMatchResults { HasProfile = false, MyIdeas = myIdeas };

        var forIdea = search.ForIdeaId.HasValue ? myIdeas.FirstOrDefault(i => i.Id == search.ForIdeaId.Value) : null;

        // Seeker: in "for idea" mode only that idea's open roles and category count.
        Dictionary<int, string> seekerCategories;
        List<OpenRoleNeed> openNeeds;
        if (forIdea != null)
        {
            var cat = await _context.Ideas.Where(i => i.Id == forIdea.Id)
                .Select(i => new CategoryRef { Id = i.CategoryId, Name = i.Category.Name }).FirstAsync();
            seekerCategories = new Dictionary<int, string> { [cat.Id] = cat.Name };
            openNeeds = forIdea.OpenNeeds;
        }
        else
        {
            seekerCategories = me.Categories.GroupBy(c => c.Id).ToDictionary(g => g.Key, g => g.First().Name);
            var ideaCats = await _context.Ideas.AsNoTracking()
                .Where(i => i.SubmitterUserId == userId && !InactiveIdeaStatuses.Contains(i.Status))
                .Select(i => new CategoryRef { Id = i.CategoryId, Name = i.Category.Name })
                .Distinct().ToListAsync();
            foreach (var c in ideaCats) seekerCategories.TryAdd(c.Id, c.Name);
            openNeeds = myIdeas.SelectMany(i => i.OpenNeeds).ToList();
        }

        var seeker = new MatchSeeker
        {
            Skills = me.Skills,
            Categories = seekerCategories,
            Availability = me.TimeAvailability,
            HoursPerWeek = me.HoursPerWeek,
            City = me.City,
            State = me.State,
            OpenNeeds = openNeeds,
            IdeaMode = forIdea != null
        };

        // People already in the seeker's network (accepted connections + teammates).
        var networkIds = await NetworkIdsAsync(userId);
        var alwaysExcluded = new HashSet<string> { userId };
        if (forIdea != null)
        {
            // Members of the idea's own team are never suggested for it.
            var teamIds = await _context.TeamMembers.Where(m => m.Team.IdeaId == forIdea.Id).Select(m => m.UserId).ToListAsync();
            alwaysExcluded.UnionWith(teamIds);
        }
        var excluded = search.IncludeConnections ? alwaysExcluded : alwaysExcluded.Union(networkIds).ToHashSet();
        var excludedList = excluded.ToList();

        var adminRoleId = await _context.Roles.Where(r => r.Name == "Admin").Select(r => r.Id).FirstOrDefaultAsync();

        var query = _context.UserProfiles.AsNoTracking()
            .WhereDiscoverable()
            .Where(p => !excludedList.Contains(p.UserId))
            .Where(p => adminRoleId == null || !_context.UserRoles.Any(ur => ur.UserId == p.UserId && ur.RoleId == adminRoleId));

        if (string.Equals(search.Role, "Investor", StringComparison.OrdinalIgnoreCase))
            query = query.Where(p => p.IsInvestor);
        else if (string.Equals(search.Role, "Founder", StringComparison.OrdinalIgnoreCase))
            query = query.Where(p => !p.IsInvestor);
        if (search.Availability.HasValue)
            query = query.Where(p => p.TimeAvailability == search.Availability.Value);
        if (search.IndustryId.HasValue)
            query = query.Where(p => p.InterestTags.Any(t => t.CategoryId == search.IndustryId.Value));
        if (!string.IsNullOrWhiteSpace(search.Skill))
        {
            var skill = search.Skill.Trim();
            query = query.Where(p => p.Skills.Any(s => s.SkillName.Contains(skill)));
        }

        // Cheap SQL pre-rank so the capped candidate set holds the most promising people.
        var myFamilies = RoleFamilies.ClassifyAll(me.Skills);
        var wantedFamilies = openNeeds.Select(n => n.Family)
            .Concat(forIdea == null ? Enum.GetValues<RoleFamily>().Where(f => !myFamilies.Contains(f)) : Enumerable.Empty<RoleFamily>())
            .Distinct().ToList();
        var wantedSkills = RoleFamilies.SkillNamesFor(wantedFamilies);
        var categoryIds = seekerCategories.Keys.ToList();

        var rows = await query
            .OrderByDescending(p => (p.Skills.Any(s => wantedSkills.Contains(s.SkillName)) ? 2 : 0)
                                  + (p.InterestTags.Any(t => categoryIds.Contains(t.CategoryId)) ? 1 : 0))
            .ThenByDescending(p => p.ProfileCompletionPercent)
            .ThenBy(p => p.Id)
            .Take(CandidateCap)
            .Select(p => new CandidateRow
            {
                UserId = p.UserId,
                FullName = p.User.FullName,
                PhotoUrl = p.ProfilePhotoUrl,
                Bio = p.Bio,
                City = p.User.City,
                State = p.User.State,
                ShowLocation = p.User.Settings == null || p.User.Settings.ShowLocation,
                IsInvestor = p.IsInvestor,
                IsVerified = p.IsVerifiedFounder,
                Completion = p.ProfileCompletionPercent,
                Availability = p.TimeAvailability,
                Hours = p.HoursPerWeek,
                Skills = p.Skills.Select(s => s.SkillName).ToList(),
                Categories = p.InterestTags.Select(t => new CategoryRef { Id = t.CategoryId, Name = t.Category.Name }).ToList()
            })
            .AsSplitQuery()
            .ToListAsync();

        var ids = rows.Select(r => r.UserId).ToList();
        var lastActive = ids.Count == 0 ? new Dictionary<string, DateTime>() : await _context.UserActivities.AsNoTracking()
            .Where(a => ids.Contains(a.UserId))
            .GroupBy(a => a.UserId)
            .Select(g => new { g.Key, Last = g.Max(a => a.CreatedAt) })
            .ToDictionaryAsync(x => x.Key, x => x.Last);

        var now = DateTime.UtcNow;
        var matches = rows.Select(r =>
            {
                var city = r.ShowLocation ? r.City : null;
                var state = r.ShowLocation ? r.State : null;
                var score = TeamMatchScorer.Score(seeker, new MatchCandidate
                {
                    UserId = r.UserId,
                    Skills = r.Skills,
                    Categories = r.Categories.GroupBy(c => c.Id).ToDictionary(g => g.Key, g => g.First().Name),
                    Availability = r.Availability,
                    HoursPerWeek = r.Hours,
                    City = city,
                    State = state,
                    IsVerified = r.IsVerified,
                    ProfileCompletion = r.Completion,
                    LastActiveAt = lastActive.TryGetValue(r.UserId, out var la) ? la : null
                }, now);
                var location = string.Join(", ", new[] { city, state }.Where(v => !string.IsNullOrWhiteSpace(v)));
                return new TeamMatch
                {
                    UserId = r.UserId,
                    FullName = r.FullName,
                    PhotoUrl = r.PhotoUrl,
                    Bio = r.Bio,
                    Location = location.Length == 0 ? null : location,
                    IsInvestor = r.IsInvestor,
                    IsVerified = r.IsVerified,
                    Availability = r.Availability,
                    HoursPerWeek = r.Hours,
                    Skills = r.Skills,
                    Interests = r.Categories.Select(c => c.Name).Distinct().ToList(),
                    Match = score
                };
            })
            .Where(m => m.Score >= minScore)
            .OrderByDescending(m => m.Score)
            .ThenByDescending(m => m.Match.Complementarity)
            .ThenByDescending(m => m.IsVerified)
            .ThenBy(m => m.FullName)
            .Take(count)
            .ToList();

        var networkExcluded = 0;
        if (!search.IncludeConnections && networkIds.Count > 0)
            networkExcluded = networkIds.Count(id => !alwaysExcluded.Contains(id));

        return new TeamMatchResults
        {
            Matches = matches,
            OpenNeeds = openNeeds,
            MyIdeas = myIdeas,
            ForIdea = forIdea,
            NetworkExcluded = networkExcluded
        };
    }

    /// <summary>The user's live ideas (not rejected/unpublished) with their open role needs, approved + newest first.</summary>
    private async Task<List<SeekerIdea>> LoadSeekerIdeasAsync(string userId)
    {
        var ideas = await _context.Ideas.AsNoTracking()
            .Where(i => i.SubmitterUserId == userId && !InactiveIdeaStatuses.Contains(i.Status))
            .OrderByDescending(i => i.Status == IdeaStatus.Approved)
            .ThenByDescending(i => i.CreatedAt)
            .Select(i => new
            {
                i.Id,
                i.Title,
                i.Status,
                Roles = i.RolesNeeded.Select(r => r.RoleName).ToList(),
                TeamRoles = i.Team == null
                    ? new List<string>()
                    : i.Team.Members.Where(m => m.UserId != userId).Select(m => m.Role).ToList()
            })
            .AsSplitQuery()
            .ToListAsync();

        return ideas.Select(i =>
        {
            var title = ShortTitle(i.Title);
            return new SeekerIdea(i.Id, i.Title, i.Status, i.Roles, TeamMatchScorer.OpenNeeds(i.Id, title, i.Roles, i.TeamRoles), i.TeamRoles);
        }).ToList();
    }

    /// <summary>"FarmConnect — Direct Farm to Home" → "FarmConnect" for compact reasons.</summary>
    private static string ShortTitle(string title)
    {
        foreach (var sep in new[] { " — ", " – ", " - ", ": " })
        {
            var idx = title.IndexOf(sep, StringComparison.Ordinal);
            if (idx > 2) return title[..idx];
        }
        return title;
    }

    private async Task<HashSet<string>> NetworkIdsAsync(string userId)
    {
        var connected = await _context.Connections.AsNoTracking()
            .Where(c => c.Status == ConnectionStatus.Accepted && (c.RequesterId == userId || c.AddresseeId == userId))
            .Select(c => c.RequesterId == userId ? c.AddresseeId : c.RequesterId)
            .ToListAsync();
        var myTeams = _context.TeamMembers.Where(m => m.UserId == userId).Select(m => m.TeamId);
        var teammates = await _context.TeamMembers.AsNoTracking()
            .Where(m => myTeams.Contains(m.TeamId) && m.UserId != userId)
            .Select(m => m.UserId)
            .ToListAsync();
        return connected.Concat(teammates).ToHashSet();
    }

    // ======================================================================
    // Similar ideas
    // ======================================================================

    public async Task<List<SimilarIdeaMatch>> GetSimilarIdeasAsync(int ideaId, int count = 4, string? excludeOwnerId = null)
    {
        var result = await GetSimilarIdeasForAsync(new[] { ideaId }, count, excludeOwnerId);
        return result.TryGetValue(ideaId, out var list) ? list : new List<SimilarIdeaMatch>();
    }

    public async Task<Dictionary<int, List<SimilarIdeaMatch>>> GetSimilarIdeasForAsync(IReadOnlyCollection<int> ideaIds, int count = 5, string? excludeOwnerId = null)
    {
        var output = new Dictionary<int, List<SimilarIdeaMatch>>();
        if (ideaIds.Count == 0) return output;
        var queryIds = ideaIds.Distinct().ToList();

        var queryVectors = await LoadVectorsAsync(_context.Ideas.Where(i => queryIds.Contains(i.Id)));
        if (queryVectors.Count == 0) return output;
        var categoryIds = queryVectors.Select(v => v.CategoryId).Distinct().ToList();

        // Candidate pool, pre-filtered in SQL: recent approved ideas in the same categories first, then other categories.
        var approved = _context.Ideas.AsNoTracking()
            .Where(i => i.Status == IdeaStatus.Approved && !queryIds.Contains(i.Id));
        if (excludeOwnerId != null) approved = approved.Where(i => i.SubmitterUserId != excludeOwnerId);

        var sameCategory = await LoadVectorsAsync(approved.Where(i => categoryIds.Contains(i.CategoryId))
            .OrderByDescending(i => i.PublishedAt).Take(SimilarPoolPerBucket));
        var otherCategories = await LoadVectorsAsync(approved.Where(i => !categoryIds.Contains(i.CategoryId))
            .OrderByDescending(i => i.PublishedAt).Take(SimilarPoolPerBucket));
        var pool = sameCategory.Concat(otherCategories).ToList();
        if (pool.Count == 0) return output;

        var idf = IdeaSimilarityScorer.InverseDocumentFrequencies(pool.Concat(queryVectors).ToList());
        var ranked = queryVectors.ToDictionary(q => q.IdeaId, q => IdeaSimilarityScorer.Rank(q, pool, idf, count));

        var matchedIds = ranked.Values.SelectMany(r => r.Select(x => x.IdeaId)).Distinct().ToList();
        if (matchedIds.Count == 0) return output;

        var display = await _context.Ideas.AsNoTracking()
            .Where(i => matchedIds.Contains(i.Id))
            .Select(i => new
            {
                i.Id, i.Title, i.Tagline, i.MinimumFundRequired, i.SubmitterUserId,
                CategoryName = i.Category.Name, CategoryIcon = i.Category.IconClass,
                SubmitterName = i.Submitter.FullName,
                Verified = i.Submitter.Profile != null && i.Submitter.Profile.IsVerifiedFounder
            })
            .ToDictionaryAsync(i => i.Id);

        foreach (var (queryId, results) in ranked)
        {
            output[queryId] = results.Where(r => display.ContainsKey(r.IdeaId)).Select(r =>
            {
                var d = display[r.IdeaId];
                return new SimilarIdeaMatch
                {
                    IdeaId = d.Id,
                    Title = d.Title,
                    Tagline = d.Tagline,
                    CategoryName = d.CategoryName,
                    CategoryIcon = d.CategoryIcon,
                    SubmitterId = d.SubmitterUserId,
                    SubmitterName = d.SubmitterName,
                    SubmitterVerified = d.Verified,
                    MinimumFundRequired = d.MinimumFundRequired,
                    Score = r.Score,
                    SameCategory = r.SameCategory,
                    SimilarMarket = r.SimilarMarket,
                    CommonKeywords = r.CommonKeywords
                };
            }).ToList();
        }
        return output;
    }

    /// <summary>
    /// Term vectors for the ideas in <paramref name="source"/>. Only (Id, UpdatedAt) is read first; texts are loaded just for
    /// ideas whose vector isn't cached (cache key includes UpdatedAt, so edits invalidate automatically).
    /// </summary>
    private async Task<List<IdeaTermVector>> LoadVectorsAsync(IQueryable<Idea> source)
    {
        var stamps = await source.Select(i => new { i.Id, i.UpdatedAt }).ToListAsync();
        var vectors = new List<IdeaTermVector>(stamps.Count);
        var missing = new List<int>();
        foreach (var s in stamps)
        {
            if (_cache.TryGetValue(CacheKey(s.Id, s.UpdatedAt), out IdeaTermVector? v) && v != null) vectors.Add(v);
            else missing.Add(s.Id);
        }
        if (missing.Count == 0) return vectors;

        var texts = await _context.Ideas.AsNoTracking()
            .Where(i => missing.Contains(i.Id))
            .Select(i => new
            {
                i.UpdatedAt,
                Fields = new IdeaTextFields
                {
                    Id = i.Id, CategoryId = i.CategoryId, Title = i.Title, Tagline = i.Tagline,
                    ProblemStatement = i.ProblemStatement, Solution = i.Solution, Description = i.Description,
                    TargetMarket = i.TargetMarket, BusinessModel = i.BusinessModel
                }
            })
            .ToListAsync();
        foreach (var t in texts)
        {
            var v = IdeaTextAnalyzer.Vectorize(t.Fields);
            _cache.Set(CacheKey(t.Fields.Id, t.UpdatedAt), v, new MemoryCacheEntryOptions { SlidingExpiration = TimeSpan.FromHours(6) });
            vectors.Add(v);
        }
        return vectors;
    }

    private static string CacheKey(int id, DateTime updatedAt) => $"idea-terms:{id}:{updatedAt.Ticks}";

    // ======================================================================
    // Investors
    // ======================================================================

    public async Task<List<UserProfile>> GetRecommendedInvestorsAsync(string userId, int count = 10)
    {
        var maxFundingNeeded = await _context.Ideas
            .Where(i => i.SubmitterUserId == userId && i.Status == IdeaStatus.Approved)
            .MaxAsync(i => (decimal?)i.MinimumFundRequired) ?? 0m;

        if (maxFundingNeeded == 0) return new List<UserProfile>();

        var requiredCapacity = GetCapacityForAmount(maxFundingNeeded);

        return await _context.UserProfiles
            .Include(p => p.User).ThenInclude(u => u.Settings)
            .Include(p => p.InterestTags).ThenInclude(t => t.Category)
            .Where(p => p.UserId != userId && p.IsInvestor && p.InvestmentCapacity >= requiredCapacity)
            .WhereDiscoverable()
            .OrderByDescending(p => p.InvestmentCapacity)
            .ThenByDescending(p => p.ProfileCompletionPercent)
            .Take(count)
            .AsSplitQuery()
            .ToListAsync();
    }

    public async Task<List<Idea>> GetRecommendedIdeasForInvestmentAsync(string userId, int count = 10)
    {
        var profile = await _context.UserProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.UserId == userId);
        if (profile == null || !profile.IsInvestor || profile.InvestmentCapacity == InvestmentCapacity.None)
            return new List<Idea>();

        var query = _context.Ideas
            .Include(i => i.Submitter)
            .Where(i => i.Status == IdeaStatus.Approved && i.SubmitterUserId != userId && i.MinimumFundRequired > 0);

        if (profile.InvestmentCapacity != InvestmentCapacity.Above1L)
        {
            var maxAmount = GetMaxAmountForCapacity(profile.InvestmentCapacity);
            query = query.Where(i => i.MinimumFundRequired <= maxAmount);
        }

        return await query.OrderByDescending(i => i.PublishedAt).Take(count).ToListAsync();
    }

    private static InvestmentCapacity GetCapacityForAmount(decimal amount)
    {
        if (amount <= 10000) return InvestmentCapacity.UpTo10K;
        if (amount <= 50000) return InvestmentCapacity.From10KTo50K;
        if (amount <= 100000) return InvestmentCapacity.From50KTo1L;
        return InvestmentCapacity.Above1L;
    }

    private static decimal GetMaxAmountForCapacity(InvestmentCapacity capacity) => capacity switch
    {
        InvestmentCapacity.UpTo10K => 10000m,
        InvestmentCapacity.From10KTo50K => 50000m,
        InvestmentCapacity.From50KTo1L => 100000m,
        InvestmentCapacity.Above1L => decimal.MaxValue,
        _ => 0m
    };
}
