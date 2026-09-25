using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StartupConnect.Data;
using StartupConnect.Models;

namespace StartupConnect.Controllers;

[Authorize]
public class InvestorController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public InvestorController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    public async Task<IActionResult> Dashboard()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        var profile = await _context.UserProfiles
            .Include(p => p.InterestTags)
            .FirstOrDefaultAsync(p => p.UserId == user.Id);
            
        if (profile == null || !profile.IsInvestor) return Forbid();

        // Get highly engaged ideas that match investor's tags
        var investorCategoryIds = profile.InterestTags.Select(t => t.CategoryId).ToList();
        
        var matchedIdeas = await _context.Ideas
            .Include(i => i.Category)
            .Include(i => i.Submitter)
            .Where(i => i.Status == IdeaStatus.Approved)
            .OrderByDescending(i => i.Views.Count * 1 + i.Likes.Count * 5 + i.Comments.Count * 10)
            .Take(10)
            .ToListAsync();
            
        ViewBag.MatchedIdeas = matchedIdeas;
        ViewBag.TotalFundsRequested = matchedIdeas.Sum(i => i.MinimumFundRequired);
        
        return View();
    }
}
