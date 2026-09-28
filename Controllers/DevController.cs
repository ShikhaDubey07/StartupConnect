using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using StartupConnect.Services.Email;

namespace StartupConnect.Controllers;

/// <summary>
/// Development-only tools. Every action returns 404 outside the Development environment.
/// /Dev/Outbox lists emails captured by <see cref="DevOutboxEmailSender"/>.
/// </summary>
public class DevController : Controller
{
    private readonly IWebHostEnvironment _env;
    private readonly IDevOutbox _outbox;

    public DevController(IWebHostEnvironment env, IDevOutbox outbox)
    {
        _env = env;
        _outbox = outbox;
    }

    public override void OnActionExecuting(ActionExecutingContext context)
    {
        if (!_env.IsDevelopment()) context.Result = NotFound();
        base.OnActionExecuting(context);
    }

    [HttpGet]
    public IActionResult Outbox(string? id)
    {
        var emails = _outbox.List();
        ViewBag.Selected = string.IsNullOrEmpty(id) ? emails.FirstOrDefault() : _outbox.Get(id);
        return View(emails);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult ClearOutbox()
    {
        _outbox.Clear();
        TempData["Success"] = "Dev outbox cleared.";
        return RedirectToAction(nameof(Outbox));
    }
}
