using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StartupConnect.Services;
using StartupConnect.ViewModels;
using System.Security.Claims;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using StartupConnect.Models;
using Microsoft.EntityFrameworkCore;

namespace StartupConnect.Controllers;

[Authorize(Roles = "Admin,Panel")]
public class AdminController : Controller
{
    private readonly IIdeaService _ideaService;
    private readonly Data.ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public AdminController(IIdeaService ideaService, Data.ApplicationDbContext context, UserManager<ApplicationUser> userManager)
    {
        _ideaService = ideaService;
        _context = context;
        _userManager = userManager;
    }

    public async Task<IActionResult> Dashboard()
    {
        var model = new AdminDashboardViewModel
        {
            TotalUsers = _context.Users.Count(),
            TotalIdeas = _context.Ideas.Count(),
            PendingIdeas = _context.Ideas.Count(i => i.Status == Models.IdeaStatus.Submitted || i.Status == Models.IdeaStatus.UnderReview),
            ApprovedIdeas = _context.Ideas.Count(i => i.Status == Models.IdeaStatus.Approved),
            TotalInterests = _context.Interests.Count(),
            PendingIdeasList = await _ideaService.GetPendingIdeasAsync()
        };
        return View(model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(int id)
    {
        if (!await _context.Ideas.AnyAsync(i => i.Id == id)) return NotFound();
        var adminId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        await _ideaService.ApproveIdeaAsync(id, adminId);
        TempData["Success"] = "Idea approved and published!";
        return RedirectToAction("Dashboard");
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(int id, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            TempData["Error"] = "Rejection reason is required.";
            return RedirectToAction("Dashboard");
        }
        if (reason.Length > 1000)
        {
            TempData["Error"] = "Rejection reason can be at most 1000 characters.";
            return RedirectToAction("Dashboard");
        }
        if (!await _context.Ideas.AnyAsync(i => i.Id == id)) return NotFound();
        var adminId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        await _ideaService.RejectIdeaAsync(id, reason, adminId);
        TempData["Success"] = "Idea rejected with feedback.";
        return RedirectToAction("Dashboard");
    }

    [HttpGet]
    public async Task<IActionResult> Users()
    {
        var users = await _userManager.Users.Include(u => u.SubmittedIdeas).ToListAsync();
        var userViewModels = new List<AdminUserViewModel>();

        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);
            userViewModels.Add(new AdminUserViewModel
            {
                Id = user.Id,
                FullName = user.FullName,
                Email = user.Email ?? string.Empty,
                Role = roles.FirstOrDefault() ?? "User",
                IsActive = user.IsActive,
                CreatedAt = user.CreatedAt,
                IdeasSubmittedCount = user.SubmittedIdeas.Count
            });
        }

        return View(userViewModels.OrderByDescending(u => u.CreatedAt).ToList());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleUserStatus(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null)
        {
            return NotFound();
        }

        // Prevent self-suspension
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (user.Id == currentUserId)
        {
            TempData["Error"] = "You cannot suspend your own account.";
            return RedirectToAction("Users");
        }

        user.IsActive = !user.IsActive;
        await _userManager.UpdateAsync(user);
        // Invalidate existing sessions so a suspension takes effect on the next request validation.
        if (!user.IsActive) await _userManager.UpdateSecurityStampAsync(user);

        TempData["Success"] = $"User {(user.IsActive ? "activated" : "suspended")} successfully.";
        return RedirectToAction("Users");
    }

    [HttpGet]
    public async Task<IActionResult> Ideas(IdeaStatus? status = null)
    {
        var query = _context.Ideas
            .Include(i => i.Submitter)
            .Include(i => i.Category)
            .AsQueryable();

        if (status.HasValue)
        {
            query = query.Where(i => i.Status == status.Value);
        }

        var ideas = await query.OrderByDescending(i => i.CreatedAt).ToListAsync();

        var viewModels = ideas.Select(i => new AdminIdeaViewModel
        {
            Id = i.Id,
            Title = i.Title,
            SubmitterName = i.Submitter.FullName,
            CategoryName = i.Category.Name,
            Status = i.Status,
            MinimumFundRequired = i.MinimumFundRequired,
            CreatedAt = i.CreatedAt
        }).ToList();

        ViewBag.CurrentStatusFilter = status;

        return View(viewModels);
    }
    
    [HttpGet]
    public async Task<IActionResult> VerificationQueue()
    {
        var users = await _userManager.Users
            .Include(u => u.Profile)
            .Where(u => u.Profile != null && u.Profile.VerificationRequested && !u.Profile.IsVerifiedFounder)
            .ToListAsync();
        return View(users);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ApproveVerification(string id)
    {
        var user = await _userManager.Users.Include(u => u.Profile).FirstOrDefaultAsync(u => u.Id == id);
        if (user != null && user.Profile != null)
        {
            user.Profile.IsVerifiedFounder = true;
            user.Profile.VerificationRequested = false;
            await _userManager.UpdateAsync(user);
        }
        TempData["Success"] = "Founder verified successfully.";
        return RedirectToAction("VerificationQueue");
    }

    [HttpGet]
    public async Task<IActionResult> Reports()
    {
        var reports = await _context.IdeaReports
            .Include(r => r.Idea)
            .Include(r => r.User)
            .ToListAsync();
        return View(reports);
    }
}
