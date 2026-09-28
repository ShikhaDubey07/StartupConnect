using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using StartupConnect.Models;

namespace StartupConnect.Infrastructure;

/// <summary>
/// Signed-in users must have confirmed their email to use the decorated action. HTML requests are
/// redirected to a friendly explainer page; AJAX requests get a JSON { success = false, message }.
/// Anonymous users fall through to the normal [Authorize] handling.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class RequireConfirmedEmailAttribute : Attribute, IAsyncActionFilter
{
    public const string Message = "Please confirm your email address first — check your inbox for the link we sent you.";

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var http = context.HttpContext;
        if (http.User.Identity?.IsAuthenticated == true)
        {
            var userManager = http.RequestServices.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await userManager.GetUserAsync(http.User);
            if (user != null && !user.EmailConfirmed)
            {
                if (IsAjax(http.Request))
                {
                    context.Result = new JsonResult(new { success = false, message = Message }) { StatusCode = StatusCodes.Status403Forbidden };
                }
                else
                {
                    context.Result = new RedirectToActionResult("EmailConfirmationRequired", "Account", null);
                }
                return;
            }
        }

        await next();
    }

    internal static bool IsAjax(HttpRequest request) =>
        request.Headers.XRequestedWith == "XMLHttpRequest"
        || request.Headers.Accept.Any(a => a != null && a.Contains("application/json", StringComparison.OrdinalIgnoreCase))
        || request.Headers.ContainsKey("RequestVerificationToken");
}
