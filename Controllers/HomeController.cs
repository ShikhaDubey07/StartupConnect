using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StartupConnect.Data;
using StartupConnect.Models;
using StartupConnect.Services;
using StartupConnect.ViewModels;

namespace StartupConnect.Controllers;

public class HomeController : Controller
{
    private readonly IIdeaService _ideaService;
    private readonly ApplicationDbContext _context;

    public HomeController(IIdeaService ideaService, ApplicationDbContext context)
    {
        _ideaService = ideaService;
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        ViewBag.FeaturedIdeas = await _ideaService.GetApprovedIdeasAsync(new IdeaBrowseViewModel { PageSize = 6 });
        ViewBag.MostLikedIdeas = await _ideaService.GetMostLikedIdeasThisWeekAsync(3);
        ViewBag.Stats = new
        {
            Users = await _context.Users.CountAsync(),
            Ideas = await _context.Ideas.CountAsync(i => i.Status == Models.IdeaStatus.Approved),
            Interests = await _context.Interests.CountAsync()
        };
        return View();
    }

    public IActionResult HowItWorks() => View();

    [HttpGet]
    public IActionResult Contact() => View(new ContactViewModel());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Contact(ContactViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        _context.ContactMessages.Add(new Models.ContactMessage
        {
            Name = model.Name,
            Email = model.Email,
            Subject = model.Subject,
            Message = model.Message
        });
        await _context.SaveChangesAsync();
        TempData["Success"] = "Thank you! We'll get back to you soon.";
        return RedirectToAction("Contact");
    }

    public IActionResult Privacy() => View();

    /// <summary>Friendly page for empty-bodied error status codes (404, 403, 429, ...).</summary>
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Status(int code)
    {
        Response.StatusCode = code is >= 400 and < 600 ? code : 404;
        ViewBag.Code = Response.StatusCode;
        return View("Status");
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() => View(new ErrorViewModel { RequestId = System.Diagnostics.Activity.Current?.Id ?? HttpContext.TraceIdentifier });
}
