namespace StartupConnect.Models;

public class UserProfile
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;

    public string? Bio { get; set; }
    public string? ProfilePhotoUrl { get; set; }
    public TimeAvailability TimeAvailability { get; set; } = TimeAvailability.PartTime;
    public int HoursPerWeek { get; set; } = 10;
    public InvestmentCapacity InvestmentCapacity { get; set; } = InvestmentCapacity.None;
    public string? LinkedInUrl { get; set; }
    public string? PortfolioUrl { get; set; }
    public bool IsInvestor { get; set; }
    public int ProfileCompletionPercent { get; set; }
    public bool IsVerifiedFounder { get; set; } = false;
    public bool VerificationRequested { get; set; } = false;

    public ICollection<UserInterestTag> InterestTags { get; set; } = new List<UserInterestTag>();
    public ICollection<UserSkill> Skills { get; set; } = new List<UserSkill>();
}
