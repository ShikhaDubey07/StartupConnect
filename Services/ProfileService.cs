using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using StartupConnect.Data;
using StartupConnect.Models;
using StartupConnect.ViewModels;
using StartupConnect.Hubs;

namespace StartupConnect.Services;

public interface IProfileService
{
    Task<ProfileViewModel?> GetProfileAsync(string userId);
    Task UpdateProfileAsync(string userId, ProfileViewModel model);
    Task<int> GetCompletionPercentAsync(string userId);
    Task<SettingsIndexViewModel> GetSettingsAsync(string userId);
    Task UpdateNotificationSettingsAsync(string userId, NotificationSettingsViewModel model);
    Task UpdatePrivacySettingsAsync(string userId, PrivacySettingsViewModel model);
    Task UpdateAccountSettingsAsync(string userId, AccountSettingsViewModel model);
}

public class ProfileService : IProfileService
{
    private readonly ApplicationDbContext _context;

    public ProfileService(ApplicationDbContext context) => _context = context;

    public async Task<ProfileViewModel?> GetProfileAsync(string userId)
    {
        var user = await _context.Users
            .Include(u => u.Profile!)
                .ThenInclude(p => p.InterestTags)
            .Include(u => u.Profile!)
                .ThenInclude(p => p.Skills)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null) return null;

        if (user.Profile == null)
        {
            user.Profile = new UserProfile { UserId = userId };
            _context.UserProfiles.Add(user.Profile);
            await _context.SaveChangesAsync();
        }

        return new ProfileViewModel
        {
            FullName = user.FullName,
            Email = user.Email ?? "",
            City = user.City,
            State = user.State,
            Age = user.Age,
            Bio = user.Profile.Bio,
            TimeAvailability = user.Profile.TimeAvailability,
            HoursPerWeek = user.Profile.HoursPerWeek,
            InvestmentCapacity = user.Profile.InvestmentCapacity,
            LinkedInUrl = user.Profile.LinkedInUrl,
            PortfolioUrl = user.Profile.PortfolioUrl,
            IsInvestor = user.Profile.IsInvestor,
            SelectedCategoryIds = user.Profile.InterestTags.Select(t => t.CategoryId).ToList(),
            SelectedSkills = user.Profile.Skills.Select(s => s.SkillName).ToList(),
            ProfileCompletionPercent = user.Profile.ProfileCompletionPercent
        };
    }

    public async Task UpdateProfileAsync(string userId, ProfileViewModel model)
    {
        var user = await _context.Users
            .Include(u => u.Profile!)
                .ThenInclude(p => p.InterestTags)
            .Include(u => u.Profile!)
                .ThenInclude(p => p.Skills)
            .FirstAsync(u => u.Id == userId);

        user.City = model.City;
        user.State = model.State;
        user.Age = model.Age;

        var profile = user.Profile ?? new UserProfile { UserId = userId };
        if (user.Profile == null) _context.UserProfiles.Add(profile);

        profile.Bio = model.Bio;
        profile.TimeAvailability = model.TimeAvailability;
        profile.HoursPerWeek = model.HoursPerWeek;
        profile.InvestmentCapacity = model.InvestmentCapacity;
        profile.LinkedInUrl = model.LinkedInUrl;
        profile.PortfolioUrl = model.PortfolioUrl;
        profile.IsInvestor = model.IsInvestor;

        // Only accept known categories/skills (the ids/names come straight from the request).
        var requestedCategoryIds = model.SelectedCategoryIds.Distinct().ToList();
        var validCategoryIds = await _context.Categories
            .Where(c => requestedCategoryIds.Contains(c.Id))
            .Select(c => c.Id)
            .ToListAsync();
        _context.UserInterestTags.RemoveRange(profile.InterestTags);
        profile.InterestTags = validCategoryIds.Select(c => new UserInterestTag { CategoryId = c }).ToList();

        _context.UserSkills.RemoveRange(profile.Skills);
        profile.Skills = model.SelectedSkills
            .Where(s => ProfileViewModel.AvailableSkills.Contains(s))
            .Distinct()
            .Select(s => new UserSkill { SkillName = s })
            .ToList();

        profile.ProfileCompletionPercent = CalculateCompletion(user, profile);
        await _context.SaveChangesAsync();
    }

    public async Task<int> GetCompletionPercentAsync(string userId)
    {
        var profile = await _context.UserProfiles.FirstOrDefaultAsync(p => p.UserId == userId);
        return profile?.ProfileCompletionPercent ?? 0;
    }

    public async Task<SettingsIndexViewModel> GetSettingsAsync(string userId)
    {
        var user = await _context.Users
            .Include(u => u.Profile!)
                .ThenInclude(p => p.InterestTags)
            .Include(u => u.Profile!)
                .ThenInclude(p => p.Skills)
            .Include(u => u.Settings)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null) return new SettingsIndexViewModel();

        var profileModel = new ProfileSettingsViewModel
        {
            FullName = user.FullName,
            City = user.City,
            State = user.State,
            Age = user.Age,
            Bio = user.Profile?.Bio,
            TimeAvailability = user.Profile?.TimeAvailability ?? TimeAvailability.PartTime,
            HoursPerWeek = user.Profile?.HoursPerWeek ?? 10,
            InvestmentCapacity = user.Profile?.InvestmentCapacity ?? InvestmentCapacity.None,
            LinkedInUrl = user.Profile?.LinkedInUrl,
            PortfolioUrl = user.Profile?.PortfolioUrl,
            IsInvestor = user.Profile?.IsInvestor ?? false
        };

        var settings = user.Settings ?? new UserSettings();

        var vm = new SettingsIndexViewModel
        {
            Profile = profileModel,
            Notifications = new NotificationSettingsViewModel
            {
                EmailNotifications = settings.EmailNotifications,
                InAppNotifications = settings.InAppNotifications,
                NotifyOnMatches = settings.NotifyOnMatches,
                NotifyOnMessages = settings.NotifyOnMessages
            },
            Privacy = new PrivacySettingsViewModel
            {
                ProfileVisibility = settings.ProfileVisibility,
                ShowEmail = settings.ShowEmail,
                ShowLocation = settings.ShowLocation,
                ShowAge = settings.ShowAge
            },
            Account = new AccountSettingsViewModel
            {
                ThemePreference = settings.ThemePreference
            }
        };

        return vm;
    }

    public async Task UpdateNotificationSettingsAsync(string userId, NotificationSettingsViewModel model)
    {
        var settings = await _context.UserSettings.FirstOrDefaultAsync(s => s.UserId == userId);
        if (settings == null)
        {
            settings = new UserSettings { UserId = userId };
            _context.UserSettings.Add(settings);
        }

        settings.EmailNotifications = model.EmailNotifications;
        settings.InAppNotifications = model.InAppNotifications;
        settings.NotifyOnMatches = model.NotifyOnMatches;
        settings.NotifyOnMessages = model.NotifyOnMessages;

        await _context.SaveChangesAsync();
    }

    public async Task UpdatePrivacySettingsAsync(string userId, PrivacySettingsViewModel model)
    {
        var settings = await _context.UserSettings.FirstOrDefaultAsync(s => s.UserId == userId);
        if (settings == null)
        {
            settings = new UserSettings { UserId = userId };
            _context.UserSettings.Add(settings);
        }

        settings.ProfileVisibility = model.ProfileVisibility;
        settings.ShowEmail = model.ShowEmail;
        settings.ShowLocation = model.ShowLocation;
        settings.ShowAge = model.ShowAge;

        await _context.SaveChangesAsync();
    }

    public async Task UpdateAccountSettingsAsync(string userId, AccountSettingsViewModel model)
    {
        var settings = await _context.UserSettings.FirstOrDefaultAsync(s => s.UserId == userId);
        if (settings == null)
        {
            settings = new UserSettings { UserId = userId };
            _context.UserSettings.Add(settings);
        }

        settings.ThemePreference = model.ThemePreference;

        await _context.SaveChangesAsync();
    }

    private static int CalculateCompletion(ApplicationUser user, UserProfile profile)
    {
        int score = 0;
        if (!string.IsNullOrWhiteSpace(user.FullName)) score += 10;
        if (!string.IsNullOrWhiteSpace(user.City)) score += 10;
        if (!string.IsNullOrWhiteSpace(user.State)) score += 10;
        if (user.Age.HasValue) score += 10;
        if (!string.IsNullOrWhiteSpace(profile.Bio)) score += 15;
        if (profile.InterestTags.Any()) score += 15;
        if (profile.Skills.Any()) score += 15;
        if (profile.InvestmentCapacity != InvestmentCapacity.None || profile.IsInvestor) score += 15;
        return Math.Min(score, 100);
    }
}

public interface IInterestService
{
    Task<(bool Success, string Message)> SubmitInterestAsync(ShowInterestViewModel model, string userId);
    Task<int> GetUserInterestCountAsync(string userId);
    Task<List<Interest>> GetIncomingRequestsAsync(string userId);
    Task<List<Interest>> GetOutgoingRequestsAsync(string userId);
    Task<bool> AcceptInterestAsync(int interestId, string userId);
    Task<bool> RejectInterestAsync(int interestId, string userId);
    Task<bool> CancelInterestAsync(int interestId, string userId);
}

public class InterestService : IInterestService
{
    private readonly ApplicationDbContext _context;
    private readonly INotificationService _notifications;
    private readonly ITeamService _teams;
    private readonly IActivityService _activity;

    public InterestService(ApplicationDbContext context, INotificationService notifications, ITeamService teams, IActivityService activity)
    {
        _context = context;
        _notifications = notifications;
        _teams = teams;
        _activity = activity;
    }

    public async Task<(bool Success, string Message)> SubmitInterestAsync(ShowInterestViewModel model, string userId)
    {
        var idea = await _context.Ideas
            .Include(i => i.Interests)
            .FirstOrDefaultAsync(i => i.Id == model.IdeaId && i.Status == IdeaStatus.Approved);

        if (idea == null) return (false, "Idea not found.");
        if (idea.SubmitterUserId == userId) return (false, "You cannot show interest in your own idea.");

        var completion = await _context.UserProfiles
            .Where(p => p.UserId == userId)
            .Select(p => p.ProfileCompletionPercent)
            .FirstOrDefaultAsync();
        if (completion < 60) return (false, "Complete at least 60% of your profile first.");

        if (model.InterestType is InterestType.Invest or InterestType.Both)
        {
            if (!model.ProposedInvestmentAmount.HasValue || model.ProposedInvestmentAmount <= 0)
                return (false, "Please enter a valid investment amount.");
        }

        var existing = await _context.Interests
            .FirstOrDefaultAsync(i => i.IdeaId == model.IdeaId && i.UserId == userId);

        if (existing != null)
        {
            existing.InterestType = model.InterestType;
            existing.ProposedInvestmentAmount = model.ProposedInvestmentAmount;
            existing.SelectedRoles = model.SelectedRoles.Any() ? string.Join(",", model.SelectedRoles) : null;
            existing.Message = model.Message;
            existing.Status = InterestStatus.Pending;
        }
        else
        {
            _context.Interests.Add(new Interest
            {
                IdeaId = model.IdeaId,
                UserId = userId,
                InterestType = model.InterestType,
                ProposedInvestmentAmount = model.ProposedInvestmentAmount,
                SelectedRoles = model.SelectedRoles.Any() ? string.Join(",", model.SelectedRoles) : null,
                Message = model.Message
            });
        }

        await _activity.RecordAsync(idea.SubmitterUserId, ActivityTypes.InterestReceived,
            $"Someone showed interest in your idea \"{idea.Title}\".", "/Interests/Manage", save: false);
        await _context.SaveChangesAsync();
        await _notifications.CreateAsync(idea.SubmitterUserId, "New Interest!",
            $"Someone showed interest in your idea '{idea.Title}'.", $"/Ideas/Detail/{idea.Id}");

        return (true, "Your interest has been submitted successfully!");
    }

    public async Task<int> GetUserInterestCountAsync(string userId)
    {
        return await _context.Interests.CountAsync(i => i.UserId == userId);
    }

    public async Task<List<Interest>> GetIncomingRequestsAsync(string userId)
    {
        return await _context.Interests
            .Include(i => i.Idea)
            .Include(i => i.User)
            .Where(i => i.Idea.SubmitterUserId == userId)
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync();
    }

    public async Task<List<Interest>> GetOutgoingRequestsAsync(string userId)
    {
        return await _context.Interests
            .Include(i => i.Idea)
                .ThenInclude(idea => idea.Submitter)
            .Where(i => i.UserId == userId)
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync();
    }

    public async Task<bool> AcceptInterestAsync(int interestId, string userId)
    {
        var interest = await _context.Interests
            .Include(i => i.Idea)
                .ThenInclude(idea => idea.Team)
            .FirstOrDefaultAsync(i => i.Id == interestId && i.Idea.SubmitterUserId == userId);

        if (interest == null || interest.Status != InterestStatus.Pending) return false;

        interest.Status = InterestStatus.Accepted;

        await _context.SaveChangesAsync();

        string? workspaceLink = null;
        if (interest.InterestType == InterestType.Work || interest.InterestType == InterestType.Both)
        {
            // Creates the team on first acceptance, always with the founder as a "Founder" member.
            var team = await _teams.EnsureTeamForIdeaAsync(interest.Idea);
            await _teams.AddMemberAsync(team, interest.UserId, interest.SelectedRoles ?? "Member");
            workspaceLink = $"/Workspace/Team/{team.Id}";
        }

        await _notifications.CreateAsync(interest.UserId, "Request Accepted!",
            workspaceLink != null
                ? $"Your collaboration request for '{interest.Idea.Title}' was accepted — welcome to the team workspace!"
                : $"Your collaboration request for '{interest.Idea.Title}' was accepted.",
            workspaceLink ?? $"/Ideas/Detail/{interest.IdeaId}");
        return true;
    }

    public async Task<bool> RejectInterestAsync(int interestId, string userId)
    {
        var interest = await _context.Interests
            .Include(i => i.Idea)
            .FirstOrDefaultAsync(i => i.Id == interestId && i.Idea.SubmitterUserId == userId);

        if (interest == null || interest.Status != InterestStatus.Pending) return false;

        interest.Status = InterestStatus.Rejected;
        await _context.SaveChangesAsync();
        await _notifications.CreateAsync(interest.UserId, "Request Update", $"Your request for '{interest.Idea.Title}' was declined.", $"/Ideas/Detail/{interest.IdeaId}");
        return true;
    }

    public async Task<bool> CancelInterestAsync(int interestId, string userId)
    {
        var interest = await _context.Interests
            .Include(i => i.Idea)
            .FirstOrDefaultAsync(i => i.Id == interestId && i.UserId == userId);

        if (interest == null || interest.Status != InterestStatus.Pending) return false;

        interest.Status = InterestStatus.Cancelled;
        await _context.SaveChangesAsync();
        await _notifications.CreateAsync(interest.Idea.SubmitterUserId, "Request Cancelled", $"A collaboration request for '{interest.Idea.Title}' was cancelled by the sender.");
        return true;
    }
}

public interface INotificationService
{
    Task CreateAsync(string userId, string title, string message, string? linkUrl = null);
    Task<List<Notification>> GetUnreadAsync(string userId);
    Task<List<Notification>> GetAllAsync(string userId);
    Task MarkAsReadAsync(int id, string userId);
    Task MarkAllAsReadAsync(string userId);
    Task<int> GetUnreadCountAsync(string userId);
}

public class NotificationService : INotificationService
{
    private readonly ApplicationDbContext _context;
    private readonly IHubContext<NotificationHub> _hubContext;

    public NotificationService(ApplicationDbContext context, IHubContext<NotificationHub> hubContext)
    {
        _context = context;
        _hubContext = hubContext;
    }

    public async Task CreateAsync(string userId, string title, string message, string? linkUrl = null)
    {
        var notification = new Notification
        {
            UserId = userId,
            Title = title,
            Message = message,
            LinkUrl = linkUrl,
            CreatedAt = DateTime.UtcNow
        };
        _context.Notifications.Add(notification);
        await _context.SaveChangesAsync();
        
        await _hubContext.Clients.User(userId).SendAsync("ReceiveNotification", new {
            id = notification.Id,
            title = notification.Title,
            message = notification.Message,
            linkUrl = notification.LinkUrl,
            createdAt = notification.CreatedAt
        });
    }

    public async Task<List<Notification>> GetUnreadAsync(string userId)
    {
        return await _context.Notifications
            .Where(n => n.UserId == userId && !n.IsRead)
            .OrderByDescending(n => n.CreatedAt)
            .Take(10)
            .ToListAsync();
    }

    public async Task<List<Notification>> GetAllAsync(string userId)
    {
        return await _context.Notifications
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .Take(50)
            .ToListAsync();
    }

    public async Task MarkAsReadAsync(int id, string userId)
    {
        var n = await _context.Notifications.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);
        if (n != null) { n.IsRead = true; await _context.SaveChangesAsync(); }
    }

    public async Task MarkAllAsReadAsync(string userId)
    {
        await _context.Notifications
            .Where(n => n.UserId == userId && !n.IsRead)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true));
    }

    public async Task<int> GetUnreadCountAsync(string userId)
    {
        return await _context.Notifications.CountAsync(n => n.UserId == userId && !n.IsRead);
    }
}
