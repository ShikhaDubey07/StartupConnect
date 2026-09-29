using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using StartupConnect.Data;
using StartupConnect.Infrastructure;
using StartupConnect.Models;
using StartupConnect.Services;

namespace StartupConnect.Controllers.Api;

/// <summary>
/// Small, read-only, anonymous JSON API over public data:
/// <list type="bullet">
/// <item><c>GET /api/categories</c> — active categories with their number of live ideas.</item>
/// <item><c>GET /api/ideas?search=&amp;categoryId=&amp;stage=&amp;page=1&amp;pageSize=20</c> — approved ideas, newest first (pageSize ≤ 50).</item>
/// <item><c>GET /api/ideas/{id}</c> — one approved idea (404 for anything not approved).</item>
/// </list>
/// Only approved ideas are exposed. Founder details respect privacy: the founder's name is included only when
/// their profile is Public, their city/state only when they also share their location; never email or age.
/// Rate limited per IP (RateLimiting:ApiPerMinute). CORS applies only when Cors:AllowedOrigins is configured.
/// </summary>
[ApiController]
[Route("api")]
[AllowAnonymous]
[Produces("application/json")]
[EnableRateLimiting(RateLimitPolicies.PublicApi)]
[EnableCors(CorsPolicies.PublicApi)]
public class PublicApiController : ControllerBase
{
    public const int MaxPageSize = 50;
    private readonly ApplicationDbContext _db;
    private readonly IAppUrls _urls;

    public PublicApiController(ApplicationDbContext db, IAppUrls urls)
    {
        _db = db;
        _urls = urls;
    }

    public sealed record CategoryDto(int Id, string Name, string? Description, string Icon, int IdeaCount);
    public sealed record FounderDto(string Name, bool Verified, string? Location);
    public sealed record IdeaSummaryDto(int Id, string Title, string Tagline, int CategoryId, string Category, string Stage,
        decimal FundingAsk, int ExpectedTeamSize, IReadOnlyList<string> RolesNeeded, int Likes, DateTime? PublishedAt, string Url, FounderDto? Founder);
    public sealed record IdeaDetailDto(int Id, string Title, string Tagline, string Description, string ProblemStatement, string Solution,
        string TargetMarket, string BusinessModel, int CategoryId, string Category, string Stage, decimal FundingAsk, int ExpectedTeamSize,
        IReadOnlyList<string> RolesNeeded, int Likes, int Comments, DateTime? PublishedAt, string Url, FounderDto? Founder);
    public sealed record PageDto<T>(int Page, int PageSize, int Total, IReadOnlyList<T> Items);

    private sealed record FounderRow(string Name, bool Verified, string? City, string? State, string? Visibility, bool? ShowLocation);

    private static FounderDto? Founder(FounderRow f)
    {
        var privacy = UserPrivacy.From(f.Visibility == null ? null : new UserSettings { ProfileVisibility = f.Visibility, ShowLocation = f.ShowLocation ?? true });
        if (privacy.Visibility != ProfileVisibilityOptions.Public) return null;
        var location = privacy.ShowLocation ? string.Join(", ", new[] { f.City, f.State }.Where(v => !string.IsNullOrWhiteSpace(v))) : "";
        return new FounderDto(f.Name, f.Verified, location.Length == 0 ? null : location);
    }

    private string IdeaUrl(int id) => _urls.Absolute($"/Ideas/Detail/{id}") ?? $"/Ideas/Detail/{id}";

    [HttpGet("categories")]
    [ResponseCache(Duration = 300, Location = ResponseCacheLocation.Any)]
    public async Task<ActionResult<IReadOnlyList<CategoryDto>>> Categories()
    {
        var items = await _db.Categories.AsNoTracking()
            .Where(c => c.IsActive)
            .OrderBy(c => c.Name)
            .Select(c => new CategoryDto(c.Id, c.Name, c.Description, c.IconClass, c.Ideas.Count(i => i.Status == IdeaStatus.Approved)))
            .ToListAsync();
        return Ok(items);
    }

    [HttpGet("ideas")]
    [ResponseCache(Duration = 60, Location = ResponseCacheLocation.Any)]
    public async Task<ActionResult<PageDto<IdeaSummaryDto>>> Ideas([FromQuery] string? search, [FromQuery] int? categoryId,
        [FromQuery] IdeaProgressStage? stage, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        var query = _db.Ideas.AsNoTracking().Where(i => i.Status == IdeaStatus.Approved);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            if (term.Length > 100) term = term[..100];
            query = query.Where(i => i.Title.Contains(term) || i.Tagline.Contains(term) || i.Description.Contains(term));
        }
        if (categoryId.HasValue) query = query.Where(i => i.CategoryId == categoryId.Value);
        if (stage.HasValue) query = query.Where(i => i.ProgressStage == stage.Value);

        var total = await query.CountAsync();
        var rows = await query
            .OrderByDescending(i => i.PublishedAt ?? i.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(i => new
            {
                i.Id, i.Title, i.Tagline, i.CategoryId, Category = i.Category.Name, i.ProgressStage, i.MinimumFundRequired,
                i.ExpectedTeamSize, Roles = i.RolesNeeded.Select(r => r.RoleName).ToList(), Likes = i.Likes.Count(), i.PublishedAt,
                Founder = new FounderRow(i.Submitter.FullName, i.Submitter.Profile != null && i.Submitter.Profile.IsVerifiedFounder,
                    i.Submitter.City, i.Submitter.State,
                    i.Submitter.Settings != null ? i.Submitter.Settings.ProfileVisibility : null,
                    i.Submitter.Settings != null ? i.Submitter.Settings.ShowLocation : null)
            })
            .ToListAsync();

        var items = rows.Select(r => new IdeaSummaryDto(r.Id, r.Title, r.Tagline, r.CategoryId, r.Category, r.ProgressStage.ToString(),
            r.MinimumFundRequired, r.ExpectedTeamSize, r.Roles, r.Likes, r.PublishedAt, IdeaUrl(r.Id), Founder(r.Founder))).ToList();
        return Ok(new PageDto<IdeaSummaryDto>(page, pageSize, total, items));
    }

    [HttpGet("ideas/{id:int}")]
    [ResponseCache(Duration = 60, Location = ResponseCacheLocation.Any)]
    public async Task<ActionResult<IdeaDetailDto>> Idea(int id)
    {
        var r = await _db.Ideas.AsNoTracking()
            .Where(i => i.Id == id && i.Status == IdeaStatus.Approved)
            .Select(i => new
            {
                i.Id, i.Title, i.Tagline, i.Description, i.ProblemStatement, i.Solution, i.TargetMarket, i.BusinessModel,
                i.CategoryId, Category = i.Category.Name, i.ProgressStage, i.MinimumFundRequired, i.ExpectedTeamSize,
                Roles = i.RolesNeeded.Select(x => x.RoleName).ToList(), Likes = i.Likes.Count(), Comments = i.Comments.Count(), i.PublishedAt,
                Founder = new FounderRow(i.Submitter.FullName, i.Submitter.Profile != null && i.Submitter.Profile.IsVerifiedFounder,
                    i.Submitter.City, i.Submitter.State,
                    i.Submitter.Settings != null ? i.Submitter.Settings.ProfileVisibility : null,
                    i.Submitter.Settings != null ? i.Submitter.Settings.ShowLocation : null)
            })
            .FirstOrDefaultAsync();
        if (r == null) return NotFound(new ProblemDetails { Status = 404, Title = "Idea not found." });

        return Ok(new IdeaDetailDto(r.Id, r.Title, r.Tagline, r.Description, r.ProblemStatement, r.Solution, r.TargetMarket, r.BusinessModel,
            r.CategoryId, r.Category, r.ProgressStage.ToString(), r.MinimumFundRequired, r.ExpectedTeamSize, r.Roles, r.Likes, r.Comments,
            r.PublishedAt, IdeaUrl(r.Id), Founder(r.Founder)));
    }
}
