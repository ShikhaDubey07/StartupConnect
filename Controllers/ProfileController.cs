using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StartupConnect.Services;
using StartupConnect.ViewModels;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using StartupConnect.Models;

namespace StartupConnect.Controllers;

[Authorize]
public class ProfileController : Controller
{
    private readonly IProfileService _profileService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly StartupConnect.Data.ApplicationDbContext _context;

    public ProfileController(IProfileService profileService, UserManager<ApplicationUser> userManager, StartupConnect.Data.ApplicationDbContext context)
    {
        _profileService = profileService;
        _userManager = userManager;
        _context = context;
    }

    public async Task<IActionResult> Detail(string id)
    {
        if (string.IsNullOrEmpty(id)) return NotFound();

        var profile = await _profileService.GetProfileAsync(id);
        if (profile == null) return NotFound();

        ViewBag.ProfileUserId = id;

        // Fetch category names for interest tags
        var categoryIds = profile.SelectedCategoryIds;
        var categories = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync(
            _context.Categories.Where(c => categoryIds.Contains(c.Id)).Select(c => c.Name));
        ViewBag.CategoryNames = categories;

        // Fetch user's approved public ideas
        var userIdeas = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync(
            _context.Ideas
                .Include(i => i.Category)
                .Include(i => i.Interests)
                .Include(i => i.Likes)
                .Where(i => i.SubmitterUserId == id && i.Status == IdeaStatus.Approved));
        ViewBag.UserIdeas = userIdeas;

        return View(profile);
    }
}
