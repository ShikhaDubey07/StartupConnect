using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StartupConnect.Infrastructure;
using StartupConnect.Services;
using StartupConnect.ViewModels;
using System.Security.Claims;

namespace StartupConnect.Controllers;

[Authorize]
public class InterestsController : Controller
{
    private readonly IInterestService _interestService;
    private readonly IPrivacyService _privacy;
    private readonly IConnectionService _connections;

    public InterestsController(IInterestService interestService, IPrivacyService privacy, IConnectionService connections)
    {
        _interestService = interestService;
        _privacy = privacy;
        _connections = connections;
    }

    [HttpPost, ValidateAntiForgeryToken]
    [RequireConfirmedEmail]
    public async Task<IActionResult> Submit(ShowInterestViewModel model)
    {
        if (!ModelState.IsValid)
            return Json(new { success = false, message = "Please fill all required fields." });

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var (success, message) = await _interestService.SubmitInterestAsync(model, userId);
        return Json(new { success, message });
    }

    public async Task<IActionResult> Manage(string? tab = null)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var incoming = await _interestService.GetIncomingRequestsAsync(userId);
        var outgoing = await _interestService.GetOutgoingRequestsAsync(userId);

        // Incoming senders reached out to this user, so PrivacyService lets the owner open their profile.
        var others = incoming.Select(r => r.OtherUserId).Concat(outgoing.Select(r => r.OtherUserId)).Distinct().ToList();
        var viewable = await _privacy.GetViewableAsync(others, userId, User.IsInRole("Admin"));
        foreach (var r in incoming.Concat(outgoing)) r.CanViewProfile = viewable.Contains(r.OtherUserId);

        ViewBag.Incoming = incoming;
        ViewBag.Outgoing = outgoing;
        ViewBag.ConnectionStates = await _connections.GetStatesAsync(userId, incoming.Select(r => r.OtherUserId));
        ViewBag.Tab = tab == "outgoing" ? "outgoing" : "incoming";
        return View();
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Accept(int id, string? note)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var result = await _interestService.AcceptInterestAsync(id, userId, note);
        TempData[result.Succeeded ? "Success" : "Error"] = result.Message;
        return RedirectToAction("Manage");
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(int id, string? note)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var result = await _interestService.RejectInterestAsync(id, userId, note);
        TempData[result.Succeeded ? "Success" : "Error"] = result.Message;
        return RedirectToAction("Manage");
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var cancelled = await _interestService.CancelInterestAsync(id, userId);
        TempData[cancelled ? "Success" : "Error"] = cancelled ? "Request cancelled." : "Only pending requests can be cancelled.";
        return RedirectToAction("Manage", new { tab = "outgoing" });
    }
}
