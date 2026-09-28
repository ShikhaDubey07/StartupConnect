using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using StartupConnect.Data;
using StartupConnect.Models;
using StartupConnect.Services;
using StartupConnect.Services.Email;
using StartupConnect.ViewModels;

using System.Security.Claims;
using System.Text;


namespace StartupConnect.Controllers;

public class AccountController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IProfileService _profileService;
    private readonly ApplicationDbContext _context;
    private readonly IEmailSender _emailSender;
    private readonly ILogger<AccountController> _logger;

    public AccountController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        IProfileService profileService,
        ApplicationDbContext context,
        IEmailSender emailSender,
        ILogger<AccountController> logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _profileService = profileService;
        _context = context;
        _emailSender = emailSender;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Register()
    {
        ViewBag.Categories = await GetCategoriesAsync();
        return View();
    }

    [HttpPost, ValidateAntiForgeryToken]
    [EnableRateLimiting(RateLimitPolicies.EmailSend)]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.Categories = await GetCategoriesAsync();
            return View(model);
        }

        var existingUser = await _userManager.FindByEmailAsync(model.Email);
        if (existingUser != null)
        {
            ModelState.AddModelError("Email", "Email is already in use.");
            ViewBag.Categories = await GetCategoriesAsync();
            return View(model);
        }

        var user = new ApplicationUser
        {
            UserName = model.Email,
            Email = model.Email,
            PhoneNumber = model.PhoneNumber,
            FullName = model.FullName,
            EmailConfirmed = false
        };

        var result = await _userManager.CreateAsync(user, model.Password);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
                ModelState.AddModelError("", error.Description);
            ViewBag.Categories = await GetCategoriesAsync();
            return View(model);
        }

        await _userManager.AddToRoleAsync(user, "Member");
        if (model.Role == "Investor") await _userManager.AddToRoleAsync(user, "Investor");

        await _profileService.UpdateProfileAsync(user.Id, new ProfileViewModel
        {
            FullName = model.FullName,
            IsInvestor = (model.Role == "Investor"),
            City = model.Location,
            TimeAvailability = model.TimeAvailability,
            InvestmentCapacity = model.InvestmentCapacity,
            SelectedCategoryIds = model.SelectedCategoryIds,
            SelectedSkills = model.SelectedSkills
        });

        await SendConfirmationEmailAsync(user);

        await _signInManager.SignInAsync(user, isPersistent: false);
        return RedirectToAction(nameof(CheckEmail));
    }

    // ---------------- Email confirmation ----------------

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> CheckEmail()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return RedirectToAction(nameof(Login));
        if (user.EmailConfirmed) return RedirectToAction(nameof(Dashboard));
        ViewBag.Email = user.Email;
        return View();
    }

    [HttpGet]
    public async Task<IActionResult> ConfirmEmail(string? userId, string? code)
    {
        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(code))
            return View(new ConfirmEmailResultViewModel { Succeeded = false });

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
            return View(new ConfirmEmailResultViewModel { Succeeded = false });

        if (user.EmailConfirmed)
            return View(new ConfirmEmailResultViewModel { Succeeded = true, AlreadyConfirmed = true, Email = user.Email });

        IdentityResult result;
        try
        {
            var decoded = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(code));
            result = await _userManager.ConfirmEmailAsync(user, decoded);
        }
        catch (FormatException)
        {
            result = IdentityResult.Failed();
        }

        if (result.Succeeded && User.Identity?.IsAuthenticated == true && _userManager.GetUserId(User) == user.Id)
            await _signInManager.RefreshSignInAsync(user);

        return View(new ConfirmEmailResultViewModel { Succeeded = result.Succeeded, Email = user.Email });
    }

    [HttpGet]
    public async Task<IActionResult> ResendConfirmation()
    {
        var model = new ResendConfirmationViewModel();
        if (User.Identity?.IsAuthenticated == true)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user?.EmailConfirmed == true) return RedirectToAction(nameof(Dashboard));
            model.Email = user?.Email ?? string.Empty;
        }
        return View(model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    [EnableRateLimiting(RateLimitPolicies.EmailSend)]
    public async Task<IActionResult> ResendConfirmation(ResendConfirmationViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var user = await _userManager.FindByEmailAsync(model.Email);
        if (user != null && !user.EmailConfirmed)
            await SendConfirmationEmailAsync(user);

        // Same response whether or not the account exists, to avoid leaking registered emails.
        model.Sent = true;
        return View(model);
    }

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> EmailConfirmationRequired()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return RedirectToAction(nameof(Login));
        if (user.EmailConfirmed) return RedirectToAction(nameof(Dashboard));
        ViewBag.Email = user.Email;
        return View();
    }

    [HttpGet]
    public IActionResult AccessDenied() => View();

    private async Task SendConfirmationEmailAsync(ApplicationUser user)
    {
        try
        {
            var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
            var code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
            var url = Url.Action(nameof(ConfirmEmail), "Account", new { userId = user.Id, code }, Request.Scheme)!;
            await _emailSender.SendAsync(EmailTemplates.ConfirmEmail(user.Email!, user.FullName, url));
        }
        catch (Exception ex)
        {
            // Registration must not fail because the mail server is down; the user can resend later.
            _logger.LogError(ex, "Failed to send confirmation email to user {UserId}", user.Id);
        }
    }

    [HttpGet]
    public IActionResult ForgotPassword()
    {
        return View();
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
    {
        if (ModelState.IsValid)
        {
            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user != null)
            {
                // In a real application, we would generate a token and send an email here.
                return RedirectToAction("ForgotPasswordConfirmation");
            }
            
            ModelState.AddModelError("Email", "Email address not found.");
        }
        return View(model);
    }

    [HttpGet]
    public IActionResult ForgotPasswordConfirmation()
    {
        return View();
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        return View();
    }

    [HttpPost, ValidateAntiForgeryToken]
    [EnableRateLimiting(RateLimitPolicies.Login)]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        if (!ModelState.IsValid) return View(model);

        var result = await _signInManager.PasswordSignInAsync(model.Email, model.Password, model.RememberMe, lockoutOnFailure: true);
        if (result.IsLockedOut)
        {
            var lockedUser = await _userManager.FindByEmailAsync(model.Email);
            var end = lockedUser == null ? null : await _userManager.GetLockoutEndDateAsync(lockedUser);
            var minutes = end.HasValue ? Math.Max(1, (int)Math.Ceiling((end.Value - DateTimeOffset.UtcNow).TotalMinutes)) : 15;
            ModelState.AddModelError("", $"Too many failed sign-in attempts. For your security this account is locked — please try again in {minutes} minute{(minutes == 1 ? "" : "s")}, or reset your password.");
            _logger.LogWarning("Account locked out after failed sign-in attempts: {Email}", model.Email);
            return View(model);
        }
        if (result.IsNotAllowed)
        {
            ModelState.AddModelError("", "This account has been suspended. Please contact support if you think this is a mistake.");
            return View(model);
        }
        if (!result.Succeeded)
        {
            ModelState.AddModelError("", "Invalid email or password.");
            return View(model);
        }

        var user = await _userManager.FindByEmailAsync(model.Email);
        if (user != null && (await _userManager.IsInRoleAsync(user, "Admin") || await _userManager.IsInRoleAsync(user, "Panel")))
        {
            if (string.IsNullOrEmpty(returnUrl) || !Url.IsLocalUrl(returnUrl))
            {
                return RedirectToAction("Dashboard", "Admin");
            }
        }

        return RedirectToLocal(returnUrl);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return RedirectToAction("Login", "Account");
    }

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> Profile()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var model = await _profileService.GetProfileAsync(userId);
        ViewBag.Categories = await GetCategoriesAsync();
        return View(model);
    }

    [Authorize]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Profile(ProfileViewModel model)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.Categories = await GetCategoriesAsync();
            return View(model);
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        await _profileService.UpdateProfileAsync(userId, model);
        TempData["Success"] = "Profile updated successfully!";
        return RedirectToAction("Profile");
    }

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> Dashboard()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction("Login", "Account");
        }

        var ideaService = HttpContext.RequestServices.GetRequiredService<IIdeaService>();
        var interestService = HttpContext.RequestServices.GetRequiredService<IInterestService>();
        var notificationService = HttpContext.RequestServices.GetRequiredService<INotificationService>();

        var model = new DashboardViewModel
        {
            UserName = user!.FullName,
            ProfileCompletion = await _profileService.GetCompletionPercentAsync(userId),
            MyIdeasCount = (await ideaService.GetUserIdeasAsync(userId)).Count,
            MyInterestsCount = await interestService.GetUserInterestCountAsync(userId),
            UnreadNotifications = await notificationService.GetUnreadCountAsync(userId),
            FeaturedIdeas = await ideaService.GetApprovedIdeasAsync(new IdeaBrowseViewModel { PageSize = 3 }),
            MyIdeas = await ideaService.GetUserIdeasAsync(userId)
        };
        return View(model);
    }

    private IActionResult RedirectToLocal(string? returnUrl)
    {
        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);
        return RedirectToAction("Dashboard");
    }

    private Task<List<Category>> GetCategoriesAsync()
        => _context.Categories.Where(c => c.IsActive).ToListAsync();
}
