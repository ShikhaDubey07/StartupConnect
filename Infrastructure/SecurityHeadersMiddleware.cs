namespace StartupConnect.Infrastructure;

/// <summary>
/// Adds baseline security headers to every response and marks authenticated HTML pages as
/// non-cacheable (so personal pages aren't served from the back/forward or proxy cache).
/// Static files are unaffected by the no-store rule and keep their own caching headers.
/// </summary>
public sealed class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;

    public SecurityHeadersMiddleware(RequestDelegate next) => _next = next;

    public Task Invoke(HttpContext context)
    {
        context.Response.OnStarting(() =>
        {
            var headers = context.Response.Headers;
            headers["X-Content-Type-Options"] = "nosniff";
            headers["X-Frame-Options"] = "DENY";
            headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
            headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), payment=()";
            headers["Content-Security-Policy"] = "frame-ancestors 'none'; object-src 'none'; base-uri 'self'; form-action 'self'";

            var contentType = context.Response.ContentType ?? string.Empty;
            if (context.User.Identity?.IsAuthenticated == true
                && contentType.StartsWith("text/html", StringComparison.OrdinalIgnoreCase)
                && !headers.ContainsKey("Cache-Control"))
            {
                headers["Cache-Control"] = "no-store, no-cache, must-revalidate";
                headers["Pragma"] = "no-cache";
            }
            return Task.CompletedTask;
        });

        return _next(context);
    }
}
