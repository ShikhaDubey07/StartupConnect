using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StartupConnect.Services;
using StartupConnect.ViewModels;
using System.Security.Claims;

namespace StartupConnect.Controllers;

[Authorize]
public class InterestsController : Controller
{
    private readonly IInterestService _interestService;

    public InterestsController(IInterestService interestService) => _interestService = interestService;

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Submit(ShowInterestViewModel model)
    {
        if (!ModelState.IsValid)
            return Json(new { success = false, message = "Please fill all required fields." });

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var (success, message) = await _interestService.SubmitInterestAsync(model, userId);
        return Json(new { success, message });
    }

    public async Task<IActionResult> Manage()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        ViewBag.Incoming = await _interestService.GetIncomingRequestsAsync(userId);
        ViewBag.Outgoing = await _interestService.GetOutgoingRequestsAsync(userId);
        return View();
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Accept(int id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        await _interestService.AcceptInterestAsync(id, userId);
        TempData["Success"] = "Request accepted successfully.";
        return RedirectToAction("Manage");
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(int id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        await _interestService.RejectInterestAsync(id, userId);
        TempData["Success"] = "Request rejected.";
        return RedirectToAction("Manage");
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        await _interestService.CancelInterestAsync(id, userId);
        TempData["Success"] = "Request cancelled.";
        return RedirectToAction("Manage");
    }
}
