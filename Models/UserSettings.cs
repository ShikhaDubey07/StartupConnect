namespace StartupConnect.Models;

public class UserSettings
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;

    // Notification Preferences
    public bool EmailNotifications { get; set; } = true;
    public bool InAppNotifications { get; set; } = true;
    public bool NotifyOnMatches { get; set; } = true;
    public bool NotifyOnMessages { get; set; } = true;

    // Privacy Settings
    public string ProfileVisibility { get; set; } = "Public"; // Public, RegisteredUsers, Private
    public bool ShowEmail { get; set; } = false;
    public bool ShowLocation { get; set; } = true;
    public bool ShowAge { get; set; } = false;

    // Account Preferences
    public string ThemePreference { get; set; } = "Light"; // Light, Dark, System
}
