using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using StartupConnect.Data;
using StartupConnect.Models;
using StartupConnect.Infrastructure;
using StartupConnect.Services;
using StartupConnect.Services.Email;
using StartupConnect.ViewModels;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;

namespace StartupConnect.Controllers;

[Authorize]
public class SettingsController : Controller
{
    private readonly IProfileService _profileService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly ApplicationDbContext _context;

    public SettingsController(IProfileService profileService, UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager, ApplicationDbContext context)
    {
        _profileService = profileService;
        _userManager = userManager;
        _signInManager = signInManager;
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string tab = "profile")
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var vm = await _profileService.GetSettingsAsync(userId);
        vm.ActiveTab = tab;
        
        // Populate viewbag for profile dropdowns
        ViewBag.Categories = await _context.Categories.Where(c => c.IsActive).ToListAsync();
        ViewBag.Skills = ProfileViewModel.AvailableSkills;
        
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateProfile(SettingsIndexViewModel model)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        
        // We only validate the Profile part
        ModelState.Clear();
        if (TryValidateModel(model.Profile))
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user != null)
            {
                user.FullName = model.Profile.FullName;
                await _userManager.UpdateAsync(user);

                // Map ProfileSettingsViewModel to ProfileViewModel to use existing method
                var pvm = new ProfileViewModel
                {
                    City = model.Profile.City,
                    State = model.Profile.State,
                    Age = model.Profile.Age,
                    Bio = model.Profile.Bio,
                    TimeAvailability = model.Profile.TimeAvailability,
                    HoursPerWeek = model.Profile.HoursPerWeek,
                    InvestmentCapacity = model.Profile.InvestmentCapacity,
                    LinkedInUrl = model.Profile.LinkedInUrl,
                    PortfolioUrl = model.Profile.PortfolioUrl,
                    IsInvestor = model.Profile.IsInvestor,
                    SelectedCategoryIds = Request.Form["Profile.SelectedCategoryIds"].Select(s => int.TryParse(s, out var i) ? i : 0).Where(i => i > 0).ToList(),
                    SelectedSkills = Request.Form["Profile.SelectedSkills"].Where(s => !string.IsNullOrEmpty(s)).Select(s => s!).ToList()
                };

                await _profileService.UpdateProfileAsync(userId, pvm);
                TempData["Success"] = "Profile information updated successfully.";
                return RedirectToAction(nameof(Index), new { tab = "profile" });
            }
        }
        
        var fullModel = await _profileService.GetSettingsAsync(userId);
        fullModel.ActiveTab = "profile";
        fullModel.Profile = model.Profile; // Keep the submitted invalid data
        
        ViewBag.Categories = await _context.Categories.Where(c => c.IsActive).ToListAsync();
        ViewBag.Skills = ProfileViewModel.AvailableSkills;
        TempData["Error"] = "Failed to update profile. Please check the form for errors.";
        return View("Index", fullModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateSecurity(SettingsIndexViewModel model, [FromServices] IEmailQueue emailQueue, [FromServices] IAppUrls urls)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        
        ModelState.Clear();
        if (TryValidateModel(model.Security))
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user != null)
            {
                var result = await _userManager.ChangePasswordAsync(user, model.Security.CurrentPassword, model.Security.NewPassword);
                if (result.Succeeded)
                {
                    await _signInManager.RefreshSignInAsync(user);
                    if (!string.IsNullOrEmpty(user.Email))
                    {
                        await emailQueue.QueueAsync(EmailTemplates.PasswordChanged(user.Email, user.FullName,
                            urls.Absolute(Url.Action("Login", "Account")), urls.Absolute(Url.Action("ForgotPassword", "Account"))), "password-changed");
                    }
                    TempData["Success"] = "Password changed successfully. Other signed-in devices will be signed out shortly.";
                    return RedirectToAction(nameof(Index), new { tab = "security" });
                }
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError("Security.CurrentPassword", error.Description);
                }
            }
        }

        var fullModel = await _profileService.GetSettingsAsync(userId);
        fullModel.ActiveTab = "security";
        fullModel.Security = model.Security; // Keep the submitted invalid data
        
        ViewBag.Categories = await _context.Categories.Where(c => c.IsActive).ToListAsync();
        ViewBag.Skills = ProfileViewModel.AvailableSkills;
        TempData["Error"] = "Failed to change password. Please check the form for errors.";
        return View("Index", fullModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateNotifications(SettingsIndexViewModel model)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        
        ModelState.Clear();
        if (TryValidateModel(model.Notifications))
        {
            await _profileService.UpdateNotificationSettingsAsync(userId, model.Notifications);
            TempData["Success"] = "Notification preferences updated successfully.";
            return RedirectToAction(nameof(Index), new { tab = "notifications" });
        }

        var fullModel = await _profileService.GetSettingsAsync(userId);
        fullModel.ActiveTab = "notifications";
        fullModel.Notifications = model.Notifications; // Keep the submitted invalid data
        
        ViewBag.Categories = await _context.Categories.Where(c => c.IsActive).ToListAsync();
        ViewBag.Skills = ProfileViewModel.AvailableSkills;
        TempData["Error"] = "Failed to update notifications.";
        return View("Index", fullModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdatePrivacy(SettingsIndexViewModel model)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        
        ModelState.Clear();
        if (TryValidateModel(model.Privacy))
        {
            await _profileService.UpdatePrivacySettingsAsync(userId, model.Privacy);
            TempData["Success"] = "Privacy settings updated successfully.";
            return RedirectToAction(nameof(Index), new { tab = "privacy" });
        }

        var fullModel = await _profileService.GetSettingsAsync(userId);
        fullModel.ActiveTab = "privacy";
        fullModel.Privacy = model.Privacy; // Keep the submitted invalid data
        
        ViewBag.Categories = await _context.Categories.Where(c => c.IsActive).ToListAsync();
        ViewBag.Skills = ProfileViewModel.AvailableSkills;
        TempData["Error"] = "Failed to update privacy settings.";
        return View("Index", fullModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateAccount(SettingsIndexViewModel model)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        
        ModelState.Clear();
        if (TryValidateModel(model.Account))
        {
            await _profileService.UpdateAccountSettingsAsync(userId, model.Account);
            TempData["Success"] = "Account preferences updated successfully.";
            return RedirectToAction(nameof(Index), new { tab = "account" });
        }

        var fullModel = await _profileService.GetSettingsAsync(userId);
        fullModel.ActiveTab = "account";
        fullModel.Account = model.Account; // Keep the submitted invalid data
        
        ViewBag.Categories = await _context.Categories.Where(c => c.IsActive).ToListAsync();
        ViewBag.Skills = ProfileViewModel.AvailableSkills;
        TempData["Error"] = "Failed to update account preferences.";
        return View("Index", fullModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteAccount(DeleteAccountViewModel model, [FromServices] IAccountDeletionService deletionService)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction("Index", "Home");
        }

        if (!string.Equals(model.Confirmation?.Trim(), "DELETE", StringComparison.Ordinal))
        {
            TempData["Error"] = "Please type DELETE (in capitals) to confirm account deletion.";
            return RedirectToAction(nameof(Index), new { tab = "account" });
        }

        if (!await _userManager.HasPasswordAsync(user) || string.IsNullOrEmpty(model.Password)
            || !await _userManager.CheckPasswordAsync(user, model.Password))
        {
            TempData["Error"] = "That password is incorrect. Your account was not deleted.";
            return RedirectToAction(nameof(Index), new { tab = "account" });
        }

        var result = await deletionService.DeleteAccountAsync(user);
        if (!result.Succeeded)
        {
            TempData["Error"] = result.Error;
            return RedirectToAction(nameof(Index), new { tab = "account" });
        }

        // Only sign out once the account is really gone.
        await _signInManager.SignOutAsync();
        TempData["Success"] = "Your account and all associated data have been deleted. We're sorry to see you go.";
        return RedirectToAction("Index", "Home");
    }
}
