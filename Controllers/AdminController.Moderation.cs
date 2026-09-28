using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StartupConnect.Models;
using StartupConnect.ViewModels;

namespace StartupConnect.Controllers;

// Founder verification queue & idea reports (Admin / Panel).
public partial class AdminController
{
    [HttpGet]
    public async Task<IActionResult> VerificationQueue()
    {
        var pending = await _moderation.GetPendingVerificationsAsync();
        var userIds = pending.Select(p => p.UserId).ToList();
        var ideaCounts = await _context.Ideas
            .Where(i => userIds.Contains(i.SubmitterUserId) && i.Status == IdeaStatus.Approved)
            .GroupBy(i => i.SubmitterUserId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count);

        return View(new AdminVerificationViewModel
        {
            Pending = pending,
            RecentDecisions = await _moderation.GetRecentVerificationDecisionsAsync(),
            ApprovedIdeaCounts = ideaCounts
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ApproveVerification(int id)
    {
        var result = await _moderation.ApproveVerificationAsync(id, AdminId);
        TempData[result.Succeeded ? "Success" : "Error"] = result.Message;
        return RedirectToAction(nameof(VerificationQueue));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RejectVerification(int id, string? reason)
    {
        var result = await _moderation.RejectVerificationAsync(id, reason, AdminId);
        TempData[result.Succeeded ? "Success" : "Error"] = result.Message;
        return RedirectToAction(nameof(VerificationQueue));
    }

    [HttpGet]
    public async Task<IActionResult> Reports(string? view = null)
    {
        var history = string.Equals(view, "history", StringComparison.OrdinalIgnoreCase);
        return View(new AdminReportsViewModel
        {
            ShowHistory = history,
            Groups = await _moderation.GetReportGroupsAsync(openOnly: !history)
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DismissReports(int ideaId, int? reportId, string? note)
    {
        var result = await _moderation.DismissReportsAsync(ideaId, reportId, note, AdminId);
        TempData[result.Succeeded ? "Success" : "Error"] = result.Message;
        return RedirectToAction(nameof(Reports));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ResolveReports(int ideaId, string? note)
    {
        var result = await _moderation.ResolveReportsAsync(ideaId, note, AdminId);
        TempData[result.Succeeded ? "Success" : "Error"] = result.Message;
        return RedirectToAction(nameof(Reports));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> UnpublishIdea(int ideaId, string? reason)
    {
        var result = await _moderation.UnpublishIdeaAsync(ideaId, reason, AdminId);
        TempData[result.Succeeded ? "Success" : "Error"] = result.Message;
        return RedirectToAction(nameof(Reports));
    }
}
