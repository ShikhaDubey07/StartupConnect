using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StartupConnect.Data;
using StartupConnect.Models;
using StartupConnect.ViewModels;
using System.Security.Claims;

namespace StartupConnect.Controllers;

[Authorize]
public class HelpController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public HelpController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var myTickets = await _context.SupportTickets
            .Where(t => t.UserId == userId)
            .OrderByDescending(t => t.UpdatedAt)
            .ToListAsync();

        var vm = new HelpIndexViewModel
        {
            MyTickets = myTickets
        };

        return View(vm);
    }

    [HttpGet]
    public IActionResult CreateTicket()
    {
        return View(new CreateTicketViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateTicket(CreateTicketViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

        var ticket = new SupportTicket
        {
            UserId = userId,
            IssueCategory = model.IssueCategory,
            Subject = model.Subject,
            Description = model.Description,
            Status = TicketStatus.Open,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.SupportTickets.Add(ticket);
        await _context.SaveChangesAsync();

        TempData["Success"] = "Your support ticket has been submitted successfully.";
        return RedirectToAction(nameof(Ticket), new { id = ticket.Id });
    }

    [HttpGet]
    public async Task<IActionResult> Ticket(int id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

        var ticket = await _context.SupportTickets
            .Include(t => t.Messages)
                .ThenInclude(m => m.User)
            .FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId);

        if (ticket == null)
        {
            return NotFound();
        }

        var vm = new TicketDetailViewModel
        {
            Ticket = ticket,
            Messages = ticket.Messages.OrderBy(m => m.CreatedAt).ToList()
        };

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reply(int ticketId, TicketDetailViewModel model)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

        var ticket = await _context.SupportTickets.FirstOrDefaultAsync(t => t.Id == ticketId && t.UserId == userId);

        if (ticket == null)
        {
            return NotFound();
        }

        if (string.IsNullOrWhiteSpace(model.NewMessage) || model.NewMessage.Length > 2000)
        {
            TempData["Error"] = string.IsNullOrWhiteSpace(model.NewMessage) ? "Message cannot be empty." : "Messages can be at most 2000 characters.";
            return RedirectToAction(nameof(Ticket), new { id = ticketId });
        }

        var message = new SupportTicketMessage
        {
            SupportTicketId = ticketId,
            UserId = userId,
            Message = model.NewMessage.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        _context.SupportTicketMessages.Add(message);
        
        ticket.UpdatedAt = DateTime.UtcNow;
        if (ticket.Status == TicketStatus.Resolved || ticket.Status == TicketStatus.Closed)
        {
            ticket.Status = TicketStatus.Open;
        }

        await _context.SaveChangesAsync();

        TempData["Success"] = "Your reply has been added.";
        return RedirectToAction(nameof(Ticket), new { id = ticketId });
    }
}
