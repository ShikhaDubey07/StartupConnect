using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StartupConnect.Data;
using StartupConnect.Models;

namespace StartupConnect.Controllers;

public class ChallengesController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public ChallengesController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    public async Task<IActionResult> Index()
    {
        var challenges = await _context.StartupChallenges
            .Where(c => c.IsActive)
            .OrderBy(c => c.Deadline)
            .ToListAsync();
        return View(challenges);
    }

    public async Task<IActionResult> Details(int id)
    {
        var challenge = await _context.StartupChallenges
            .Include(c => c.Submissions)
            .ThenInclude(s => s.Idea)
            .FirstOrDefaultAsync(c => c.Id == id);
            
        if (challenge == null) return NotFound();

        return View(challenge);
    }
}
