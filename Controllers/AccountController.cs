using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StartupConnect.Data;
using StartupConnect.Models;
using StartupConnect.Services;
using StartupConnect.ViewModels;

using System.Security.Claims;


namespace StartupConnect.Controllers;

public class AccountController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IProfileService _profileService;
    private readonly ApplicationDbContext _context;
    private readonly IEmailSender _emailSender;

    public AccountController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        IProfileService profileService,
          
        ApplicationDbContext context,
        IEmailSender emailSender)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _profileService = profileService;
        _context = context;
        _emailSender = emailSender;
    }


    [HttpGet]
    public async Task<IActionResult> Register()
    {
        ViewBag.Categories = await GetCategoriesAsync();
        return View();
    }

    [HttpPost, ValidateAntiForgeryToken]
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
            EmailConfirmed = true
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

        await _signInManager.SignInAsync(user, isPersistent: false);
        TempData["Success"] = "Welcome to StartupConnect! Complete your profile to get started.";
        return RedirectToAction("Dashboard");
    }

    [HttpGet]
    public IActionResult ForgotPassword()
    {
        return View();
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var user = await _userManager.FindByEmailAsync(model.Email);

        // Security: don't reveal whether email exists — always redirect to confirmation
        if (user != null)
        {
            // Generate a secure, time-limited password reset token
            var token = await _userManager.GeneratePasswordResetTokenAsync(user);

            // Build the reset link with token and email embedded
            var resetLink = Url.Action(
                action: "ResetPassword",
                controller: "Account",
                values: new { token, email = user.Email },
                protocol: Request.Scheme);

            // Build HTML email body
            var emailBody = $@"
                <h2>Password Reset — StartupConnect</h2>
                <p>Hello {user.FullName},</p>
                <p>We received a request to reset your password. Click the button below to create a new password:</p>
                <p>
                    <a href='{resetLink}' 
                       style='background:#4F46E5;color:#fff;padding:12px 24px;border-radius:6px;text-decoration:none;display:inline-block;'>
                        Reset My Password
                    </a>
                </p>
                <p>If you did not request a password reset, you can safely ignore this email. The link will expire in 24 hours.</p>
                <hr/>
                <small>StartupConnect — Connecting Founders, Co-Founders & Investors</small>";

            try
            {
                await _emailSender.SendEmailAsync(user.Email!, "Reset Your Password — StartupConnect", emailBody);
            }
            catch
            {
                // Log is handled inside SmtpEmailSender; show generic error to user
                ModelState.AddModelError("", "Could not send reset email. Please try again later.");
                return View(model);
            }
        }

        // Always redirect to confirmation — don't leak whether email exists
        return RedirectToAction("ForgotPasswordConfirmation");
    }

    [HttpGet]
    public IActionResult ForgotPasswordConfirmation()
    {
        return View();
    }

    [HttpGet]
    public IActionResult ResetPassword(string? token, string? email)
    {
        if (token == null || email == null)
        {
            TempData["Error"] = "Invalid password reset link.";
            return RedirectToAction("Login");
        }
        var model = new ResetPasswordViewModel { Token = token, Email = email };
        return View(model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var user = await _userManager.FindByEmailAsync(model.Email);
        if (user == null)
        {
            // Don't reveal user doesn't exist — redirect to success page
            return RedirectToAction("ResetPasswordConfirmation");
        }

        var result = await _userManager.ResetPasswordAsync(user, model.Token, model.NewPassword);
        if (result.Succeeded)
        {
            return RedirectToAction("ResetPasswordConfirmation");
        }

        foreach (var error in result.Errors)
            ModelState.AddModelError("", error.Description);

        return View(model);
    }

    [HttpGet]
    public IActionResult ResetPasswordConfirmation()
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
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
    {
        if (!ModelState.IsValid) return View(model);

        var result = await _signInManager.PasswordSignInAsync(model.Email, model.Password, model.RememberMe, lockoutOnFailure: false);
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
