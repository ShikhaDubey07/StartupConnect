using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StartupConnect.Infrastructure;
using StartupConnect.Services;
using StartupConnect.ViewModels;

namespace StartupConnect.Controllers;

/// <summary>
/// "My Network": connection requests between members. Every action works both as an AJAX call (returns
/// JSON { success, message, state }) used by the shared Connect button, and as a plain form post
/// (redirects back with a toast) used on the network page.
/// </summary>
[Authorize]
public class NetworkController : Controller
{
    private readonly IConnectionService _connections;

    public NetworkController(IConnectionService connections) => _connections = connections;

    private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    [HttpGet]
    public async Task<IActionResult> Index(string? tab = null)
    {
        var model = await _connections.GetNetworkAsync(CurrentUserId);
        model.Tab = tab is "incoming" or "sent" ? tab : (tab == null && model.Incoming.Count > 0 ? "incoming" : "connections");
        return View(model);
    }

    /// <summary>The Connect button for one member, rendered with the current relationship (used after AJAX actions).</summary>
    [HttpGet]
    public async Task<IActionResult> Button(string userId, string? variant = null)
    {
        if (string.IsNullOrEmpty(userId)) return BadRequest();
        var state = await _connections.GetStateAsync(CurrentUserId, userId);
        return PartialView("_ConnectButton", new ConnectButtonModel(userId, state, null, variant == "profile" ? "profile" : "card"));
    }

    [HttpPost, ValidateAntiForgeryToken]
    [RequireConfirmedEmail]
    public async Task<IActionResult> Send(string userId, string? message, string? returnUrl = null)
        => await Respond(await _connections.SendAsync(CurrentUserId, userId, message), userId, returnUrl);

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Accept(string userId, string? returnUrl = null)
        => await Respond(await _connections.AcceptAsync(CurrentUserId, userId), userId, returnUrl);

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Decline(string userId, string? returnUrl = null)
        => await Respond(await _connections.DeclineAsync(CurrentUserId, userId), userId, returnUrl);

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Withdraw(string userId, string? returnUrl = null)
        => await Respond(await _connections.WithdrawAsync(CurrentUserId, userId), userId, returnUrl);

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Remove(string userId, string? returnUrl = null)
        => await Respond(await _connections.RemoveAsync(CurrentUserId, userId), userId, returnUrl);

    private async Task<IActionResult> Respond(ServiceResult result, string userId, string? returnUrl)
    {
        if (RequireConfirmedEmailAttribute.IsAjax(Request))
        {
            var state = string.IsNullOrEmpty(userId) ? ConnectionState.None : await _connections.GetStateAsync(CurrentUserId, userId);
            return Json(new { success = result.Succeeded, message = result.Message, state = state.ToString() });
        }

        TempData[result.Succeeded ? "Success" : "Error"] = result.Message;
        return !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)
            ? LocalRedirect(returnUrl)
            : RedirectToAction(nameof(Index));
    }
}
