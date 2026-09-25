using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StartupConnect.Services;
using System.Security.Claims;

namespace StartupConnect.Controllers;

[Authorize]
public class NotificationsController : Controller
{
    private readonly INotificationService _notificationService;

    public NotificationsController(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var notifications = await _notificationService.GetAllAsync(userId);
        return View(notifications);
    }

    [HttpGet]
    public async Task<IActionResult> Unread()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var items = await _notificationService.GetUnreadAsync(userId);
        return Json(items.Select(n => new { n.Id, n.Title, n.Message, n.LinkUrl, n.CreatedAt }));
    }

    [HttpPost]
    public async Task<IActionResult> MarkRead(int id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        await _notificationService.MarkAsReadAsync(id, userId);
        return Ok();
    }

    [HttpPost]
    public async Task<IActionResult> MarkAllAsRead()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var unread = await _notificationService.GetUnreadAsync(userId);
        foreach (var n in unread)
        {
            await _notificationService.MarkAsReadAsync(n.Id, userId);
        }
        return RedirectToAction("Index");
    }
}
