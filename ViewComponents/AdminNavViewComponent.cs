using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StartupConnect.Data;
using StartupConnect.Models;
using StartupConnect.Services;

namespace StartupConnect.ViewComponents;

public sealed class AdminNavModel
{
    public string Active { get; init; } = string.Empty;
    public int PendingIdeas { get; init; }
    public ModerationCounts Counts { get; init; } = new();
}

/// <summary>Admin area navigation with live pending counts. Usage: @await Component.InvokeAsync("AdminNav", new { active = "Reports" })</summary>
public sealed class AdminNavViewComponent : ViewComponent
{
    private readonly ApplicationDbContext _db;
    private readonly IModerationService _moderation;

    public AdminNavViewComponent(ApplicationDbContext db, IModerationService moderation)
    {
        _db = db;
        _moderation = moderation;
    }

    public async Task<IViewComponentResult> InvokeAsync(string active)
    {
        return View(new AdminNavModel
        {
            Active = active,
            PendingIdeas = await _db.Ideas.CountAsync(i => i.Status == IdeaStatus.Submitted || i.Status == IdeaStatus.UnderReview),
            Counts = await _moderation.GetCountsAsync()
        });
    }
}
