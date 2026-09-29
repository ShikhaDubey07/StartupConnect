using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using StartupConnect.Data;
using StartupConnect.Hubs;
using StartupConnect.Infrastructure;
using StartupConnect.Models;
using StartupConnect.Services;
using StartupConnect.Services.Background;
using StartupConnect.Services.Email;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"),
        o => o.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery)));

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequiredLength = 8;
    options.Password.RequiredUniqueChars = 4;
    options.User.RequireUniqueEmail = true;

    options.Lockout.AllowedForNewUsers = true;
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);

    // Unconfirmed users may sign in; [RequireConfirmedEmail] gates idea submission / interest.
    options.SignIn.RequireConfirmedEmail = false;

    // Password reset links use a dedicated, short-lived token provider (see Auth:PasswordResetTokenHours).
    options.Tokens.PasswordResetTokenProvider = PasswordResetTokenProviderOptions.ProviderName;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddSignInManager<AppSignInManager>()
.AddDefaultTokenProviders()
.AddTokenProvider<PasswordResetTokenProvider<ApplicationUser>>(PasswordResetTokenProviderOptions.ProviderName);

var resetTokenHours = Math.Clamp(builder.Configuration.GetValue<int?>(PasswordResetTokenProviderOptions.ConfigKey)
    ?? PasswordResetTokenProviderOptions.DefaultHours, 1, 48);
builder.Services.Configure<PasswordResetTokenProviderOptions>(o => o.TokenLifespan = TimeSpan.FromHours(resetTokenHours));

// Re-check the security stamp / suspension status regularly so suspended users are signed out.
builder.Services.Configure<SecurityStampValidatorOptions>(o => o.ValidationInterval = TimeSpan.FromMinutes(5));

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    // Secure whenever the request is HTTPS (production forces HTTPS via redirect + HSTS).
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
});

builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "RequestVerificationToken";
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
});

// Honour X-Forwarded-Proto/For from a local reverse proxy (TLS termination) so HTTPS detection,
// secure cookies and generated links (e.g. email confirmation) use the public scheme.
builder.Services.Configure<Microsoft.AspNetCore.Builder.ForwardedHeadersOptions>(o =>
    o.ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor
                       | Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto);

builder.Services.AddScoped<IIdeaService, IdeaService>();
builder.Services.AddScoped<IProfileService, ProfileService>();
builder.Services.AddMemoryCache();
builder.Services.AddScoped<IMatchingService, MatchingService>();
builder.Services.AddScoped<IIdeaViewTracker, IdeaViewTracker>();
builder.Services.AddScoped<IInterestService, InterestService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IVideoService, VideoService>();
builder.Services.AddScoped<IAccountDeletionService, AccountDeletionService>();
builder.Services.AddScoped<IActivityService, ActivityService>();
builder.Services.AddScoped<ITeamService, TeamService>();
builder.Services.AddScoped<IChallengeService, ChallengeService>();
builder.Services.AddScoped<IModerationService, ModerationService>();
builder.Services.AddScoped<IPrivacyService, PrivacyService>();
builder.Services.AddScoped<IConnectionService, ConnectionService>();
builder.Services.AddScoped<IInvestorDashboardService, InvestorDashboardService>();

// Absolute links in emails: App:BaseUrl when configured, otherwise the current request's host.
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<IAppUrls, AppUrls>();
builder.Services.AddHttpClient<IAIAnalysisService, AIAnalysisService>();

// Background work (AI analysis etc.) runs on a Channel-backed queue with its own DI scopes.
builder.Services.AddSingleton<IBackgroundTaskQueue>(_ => new BackgroundTaskQueue(capacity: 200));
builder.Services.AddHostedService<QueuedHostedService>();
builder.Services.AddSingleton<IEmailQueue, EmailQueue>();
builder.Services.AddSingleton<IAnalysisJobTracker, AnalysisJobTracker>();
builder.Services.AddSingleton<IIdeaAnalysisScheduler, IdeaAnalysisScheduler>();

// Email: real SMTP when configured outside Development, otherwise log + dev outbox (/Dev/Outbox).
builder.Services.Configure<EmailOptions>(builder.Configuration.GetSection(EmailOptions.SectionName));
var emailOptions = builder.Configuration.GetSection(EmailOptions.SectionName).Get<EmailOptions>() ?? new EmailOptions();
var useDevOutbox = emailOptions.UseDevOutbox
    ?? (builder.Environment.IsDevelopment() || string.IsNullOrWhiteSpace(emailOptions.Smtp.Host));
builder.Services.AddSingleton<DevOutboxEmailSender>();
builder.Services.AddSingleton<IDevOutbox>(sp => sp.GetRequiredService<DevOutboxEmailSender>());
if (useDevOutbox)
    builder.Services.AddSingleton<IEmailSender>(sp => sp.GetRequiredService<DevOutboxEmailSender>());
else
    builder.Services.AddSingleton<IEmailSender, SmtpEmailSender>();

builder.Services.AddControllersWithViews(options =>
{
    // Every unsafe (POST/PUT/PATCH/DELETE) MVC request must carry a valid antiforgery token.
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
});

// Limits are configurable (RateLimiting:*). Development defaults are looser so local/browser test
// runs that register many accounts aren't throttled.
var isDevEnv = builder.Environment.IsDevelopment();
int Limit(string key, int prod, int dev) => builder.Configuration.GetValue<int?>($"RateLimiting:{key}") ?? (isDevEnv ? dev : prod);
var aiLimit = Limit("AiGeneratePerTenMinutes", 5, 5);
var emailLimit = Limit("EmailSendPerFifteenMinutes", 5, 100);
var loginLimit = Limit("LoginPerFiveMinutes", 20, 200);
var apiLimit = Limit("ApiPerMinute", 60, 600);

builder.Services.AddRateLimiter(options =>
{
    static string UserOrIp(HttpContext ctx) =>
        ctx.User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? ctx.Connection.RemoteIpAddress?.ToString()
        ?? "unknown";

    // Gemini calls cost money: at most N (default 5) regenerations per user per 10 minutes.
    options.AddPolicy(RateLimitPolicies.AiGenerate, ctx => RateLimitPartition.GetFixedWindowLimiter(
        "ai:" + UserOrIp(ctx),
        _ => new FixedWindowRateLimiterOptions { PermitLimit = aiLimit, Window = TimeSpan.FromMinutes(10), QueueLimit = 0 }));

    // Registration / resend-confirmation send emails (default 5 per 15 minutes per client).
    options.AddPolicy(RateLimitPolicies.EmailSend, ctx => RateLimitPartition.GetFixedWindowLimiter(
        "email:" + (ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown"),
        _ => new FixedWindowRateLimiterOptions { PermitLimit = emailLimit, Window = TimeSpan.FromMinutes(15), QueueLimit = 0 }));

    // Login attempts per IP (account lockout protects individual accounts).
    options.AddPolicy(RateLimitPolicies.Login, ctx => RateLimitPartition.GetFixedWindowLimiter(
        "login:" + (ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown"),
        _ => new FixedWindowRateLimiterOptions { PermitLimit = loginLimit, Window = TimeSpan.FromMinutes(5), QueueLimit = 0 }));

    // Public read-only JSON API (/api/*) per client IP.
    options.AddPolicy(RateLimitPolicies.PublicApi, ctx => RateLimitPartition.GetFixedWindowLimiter(
        "api:" + (ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown"),
        _ => new FixedWindowRateLimiterOptions { PermitLimit = apiLimit, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));

    options.OnRejected = async (context, ct) =>
    {
        const string message = "You're doing that too often. Please wait a few minutes and try again.";
        var http = context.HttpContext;
        http.Response.StatusCode = StatusCodes.Status429TooManyRequests;

        if (RequireConfirmedEmailAttribute.IsAjax(http.Request) || http.Request.Path.StartsWithSegments("/api"))
        {
            await http.Response.WriteAsJsonAsync(new { success = false, message }, ct);
            return;
        }

        var tempData = http.RequestServices.GetRequiredService<ITempDataDictionaryFactory>().GetTempData(http);
        tempData["Error"] = message;
        tempData.Save();

        var target = "/";
        if (Uri.TryCreate(http.Request.Headers.Referer.ToString(), UriKind.Absolute, out var referer)
            && string.Equals(referer.Authority, http.Request.Host.Value, StringComparison.OrdinalIgnoreCase)
            && UrlSafety.IsLocalPath(referer.PathAndQuery))
        {
            target = referer.PathAndQuery;
        }
        http.Response.Redirect(target);
    };
});

// CORS is off unless Cors:AllowedOrigins lists origins (e.g. a separate frontend). It only ever applies to the
// read-only public API (GET), never to the cookie-authenticated MVC pages.
var corsOrigins = (builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>())
    .Select(o => o.Trim().TrimEnd('/'))
    .Where(UrlSafety.IsSafeHttpUrl)
    .Distinct(StringComparer.OrdinalIgnoreCase)
    .ToArray();
builder.Services.AddCors(options =>
{
    if (corsOrigins.Length > 0)
        options.AddPolicy(CorsPolicies.PublicApi, policy => policy.WithOrigins(corsOrigins).WithMethods("GET").WithHeaders("Accept", "Content-Type"));
});
builder.Services.AddSignalR();

var app = builder.Build();

app.UseForwardedHeaders();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
    app.UseHttpsRedirection();
}
app.UseStatusCodePagesWithReExecute("/Home/Status", "?code={0}");

app.UseMiddleware<SecurityHeadersMiddleware>();

app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        // Fingerprinted assets (asp-append-version adds ?v=) can be cached for a long time.
        var versioned = ctx.Context.Request.Query.ContainsKey("v");
        ctx.Context.Response.Headers.CacheControl = versioned
            ? "public, max-age=31536000, immutable"
            : "public, max-age=86400";
    }
});
app.UseRouting();
// Without Cors:AllowedOrigins no policy is registered, so no CORS headers are ever emitted.
app.UseCors();
app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapHub<NotificationHub>("/hubs/notifications");
app.MapHub<TeamHub>(TeamHub.Path);

// MUST be before app.Run() — app.Run() blocks forever
using (var scope = app.Services.CreateScope())
{
    await DbInitializer.InitializeAsync(scope.ServiceProvider);
}

if (useDevOutbox && !app.Environment.IsDevelopment())
{
    app.Logger.LogWarning("Email:Smtp:Host is not configured — emails are only written to the log and {Path}.", emailOptions.OutboxPath);
}

app.MapControllers();
app.Run();

public static class RateLimitPolicies
{
    public const string AiGenerate = "ai-generate";
    public const string EmailSend = "email-send";
    public const string Login = "login";
    public const string PublicApi = "public-api";
}

public static class CorsPolicies
{
    public const string PublicApi = "public-api";
}
