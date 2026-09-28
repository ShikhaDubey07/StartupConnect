using System.ComponentModel.DataAnnotations;
using StartupConnect.Models;

namespace StartupConnect.ViewModels;

public class SettingsIndexViewModel
{
    public ProfileSettingsViewModel Profile { get; set; } = new();
    public SecuritySettingsViewModel Security { get; set; } = new();
    public NotificationSettingsViewModel Notifications { get; set; } = new();
    public PrivacySettingsViewModel Privacy { get; set; } = new();
    public AccountSettingsViewModel Account { get; set; } = new();
    
    // Determine which tab should be active (in case of errors/redirects)
    public string ActiveTab { get; set; } = "profile"; 
}

public class ProfileSettingsViewModel
{
    [Required, Display(Name = "Full Name")]
    [StringLength(100)]
    public string FullName { get; set; } = string.Empty;

    [StringLength(100)]
    public string? City { get; set; }
    [StringLength(100)]
    public string? State { get; set; }
    
    [Range(13, 120)]
    public int? Age { get; set; }

    [StringLength(500)]
    public string? Bio { get; set; }

    [Display(Name = "Time Availability")]
    public TimeAvailability TimeAvailability { get; set; }

    [Range(1, 80)]
    [Display(Name = "Hours Per Week")]
    public int HoursPerWeek { get; set; }

    [Display(Name = "Investment Capacity")]
    public InvestmentCapacity InvestmentCapacity { get; set; }

    [Url, StringLength(300), Display(Name = "LinkedIn URL")]
    public string? LinkedInUrl { get; set; }
    
    [Url, StringLength(300), Display(Name = "Portfolio/Website URL")]
    public string? PortfolioUrl { get; set; }

    [Display(Name = "I am looking to invest")]
    public bool IsInvestor { get; set; }

    public List<int> SelectedCategoryIds { get; set; } = new();
    public List<string> SelectedSkills { get; set; } = new();
}

public class SecuritySettingsViewModel
{
    [Required]
    [DataType(DataType.Password)]
    [Display(Name = "Current Password")]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "Password must be at least 8 characters.")]
    [RegularExpression(PasswordRules.Pattern, ErrorMessage = PasswordRules.Message)]
    [DataType(DataType.Password)]
    [Display(Name = "New Password")]
    public string NewPassword { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Password)]
    [Display(Name = "Confirm New Password")]
    [Compare("NewPassword", ErrorMessage = "The new password and confirmation password do not match.")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

public class NotificationSettingsViewModel
{
    [Display(Name = "Email Notifications")]
    public bool EmailNotifications { get; set; }

    [Display(Name = "In-App Notifications")]
    public bool InAppNotifications { get; set; }

    [Display(Name = "Notify on new idea matches")]
    public bool NotifyOnMatches { get; set; }

    [Display(Name = "Notify on new messages/requests")]
    public bool NotifyOnMessages { get; set; }
}

public class PrivacySettingsViewModel
{
    [Required]
    [Display(Name = "Profile Visibility")]
    [RegularExpression("^(Public|RegisteredUsers|Private)$", ErrorMessage = "Choose a valid visibility option.")]
    public string ProfileVisibility { get; set; } = "Public";

    [Display(Name = "Show my email on my profile")]
    public bool ShowEmail { get; set; }

    [Display(Name = "Show my location")]
    public bool ShowLocation { get; set; }

    [Display(Name = "Show my age")]
    public bool ShowAge { get; set; }
}

public class AccountSettingsViewModel
{
    [Required]
    [Display(Name = "Theme Preference")]
    [RegularExpression("^(Light|Dark|System)$", ErrorMessage = "Choose a valid theme.")]
    public string ThemePreference { get; set; } = "Light";
}
