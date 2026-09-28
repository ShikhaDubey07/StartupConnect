using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StartupConnect.Data;
using StartupConnect.Models;

namespace StartupConnect.Controllers;

[Authorize]
public class WorkspaceController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public WorkspaceController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    public async Task<IActionResult> Index(int teamId)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();
        
        var team = await _context.Teams
            .Include(t => t.Members)
            .Include(t => t.Idea)
            .FirstOrDefaultAsync(t => t.Id == teamId);
            
        if (team == null) return NotFound();
        
        if (!team.Members.Any(m => m.UserId == user.Id))
            return Forbid();
            
        var messages = await _context.TeamMessages
            .Include(m => m.Sender)
            .Where(m => m.TeamId == teamId)
            .OrderByDescending(m => m.SentAt)
            .Take(50)
            .ToListAsync();
            
        var milestones = await _context.IdeaMilestones
            .Where(m => m.IdeaId == team.IdeaId)
            .OrderBy(m => m.DueDate)
            .ToListAsync();
            
        ViewBag.Team = team;
        ViewBag.Messages = messages;
        ViewBag.Milestones = milestones;

        return View();
    }
    
    public const int MaxMessageLength = 2000;

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SendMessage(int teamId, string content)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        var teamMember = await _context.TeamMembers.FirstOrDefaultAsync(m => m.TeamId == teamId && m.UserId == user.Id);
        
        if (teamMember == null) return Forbid();
        
        content = content?.Trim() ?? string.Empty;
        if (content.Length > MaxMessageLength)
        {
            TempData["Error"] = $"Messages can be at most {MaxMessageLength} characters.";
        }
        else if (content.Length > 0)
        {
            _context.TeamMessages.Add(new TeamMessage
            {
                TeamId = teamId,
                SenderUserId = user.Id,
                Content = content
            });
            await _context.SaveChangesAsync();
        }
        
        return RedirectToAction(nameof(Index), new { teamId });
    }
}
