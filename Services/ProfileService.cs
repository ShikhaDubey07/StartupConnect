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
    /// <summary>Requests on the user's ideas, with the sender's mini profile (pending first).</summary>
    Task<List<InterestRequestViewModel>> GetIncomingRequestsAsync(string userId);
    /// <summary>Requests the user sent, with the idea owner's details and any reply note.</summary>
    Task<List<InterestRequestViewModel>> GetOutgoingRequestsAsync(string userId);
    /// <summary>Owner accepts a pending request; <paramref name="note"/> (optional) is sent to the requester.</summary>
    Task<ServiceResult> AcceptInterestAsync(int interestId, string userId, string? note = null);
    Task<ServiceResult> RejectInterestAsync(int interestId, string userId, string? note = null);
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
            existing.CreatedAt = DateTime.UtcNow;
            existing.ResponseNote = null;
            existing.RespondedAt = null;
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

        // The idea owner always sees who reached out — even when the sender's profile is Private — because the
        // sender initiated contact with them (see PrivacyService: interest senders are visible to the idea owner).
        var senderName = await _context.Users.Where(u => u.Id == userId).Select(u => u.FullName).FirstOrDefaultAsync() ?? "A member";
        var what = model.InterestType switch
        {
            InterestType.Invest => $"wants to invest ₹{model.ProposedInvestmentAmount:N0} in",
            InterestType.Both => $"wants to join and invest ₹{model.ProposedInvestmentAmount:N0} in",
            _ => model.SelectedRoles.Any() ? $"wants to join as {string.Join(", ", model.SelectedRoles)} on" : "wants to work on"
        };
        await _activity.RecordAsync(idea.SubmitterUserId, ActivityTypes.InterestReceived,
            $"{senderName} {what} your idea \"{idea.Title}\".", "/Interests/Manage", save: false);
        await _context.SaveChangesAsync();
        await _notifications.CreateAsync(idea.SubmitterUserId, $"New interest from {senderName}",
            $"{senderName} {what} your idea '{idea.Title}'.", "/Interests/Manage", category: NotificationCategory.Interest);

        return (true, "Your interest has been submitted successfully!");
    }

    public async Task<int> GetUserInterestCountAsync(string userId)
    {
        return await _context.Interests.CountAsync(i => i.UserId == userId);
    }

    public async Task<List<InterestRequestViewModel>> GetIncomingRequestsAsync(string userId)
    {
        var rows = await _context.Interests.AsNoTracking()
            .Where(i => i.Idea.SubmitterUserId == userId)
            .OrderBy(i => i.Status == InterestStatus.Pending ? 0 : 1)
            .ThenByDescending(i => i.CreatedAt)
            .Select(i => new
            {
                Request = new InterestRequestViewModel
                {
                    Id = i.Id,
                    IdeaId = i.IdeaId,
                    IdeaTitle = i.Idea.Title,
                    InterestType = i.InterestType,
                    Status = i.Status,
                    ProposedInvestmentAmount = i.ProposedInvestmentAmount,
                    Message = i.Message,
                    CreatedAt = i.CreatedAt,
                    ResponseNote = i.ResponseNote,
                    RespondedAt = i.RespondedAt,
                    OtherUserId = i.UserId,
                    OtherName = i.User.FullName,
                    OtherPhotoUrl = i.User.Profile != null ? i.User.Profile.ProfilePhotoUrl : null,
                    OtherBio = i.User.Profile != null ? i.User.Profile.Bio : null,
                    OtherVerified = i.User.Profile != null && i.User.Profile.IsVerifiedFounder,
                    OtherIsInvestor = i.User.Profile != null && i.User.Profile.IsInvestor,
                    OtherAvailability = i.User.Profile != null ? i.User.Profile.TimeAvailability : null,
                    OtherHoursPerWeek = i.User.Profile != null ? i.User.Profile.HoursPerWeek : null,
                    OtherSkills = i.User.Profile != null ? i.User.Profile.Skills.Select(s => s.SkillName).ToList() : new List<string>()
                },
                i.SelectedRoles,
                i.User.City,
                i.User.State,
                ShowLocation = i.User.Settings == null || i.User.Settings.ShowLocation
            })
            .AsSplitQuery()
            .ToListAsync();

        return rows.Select(r =>
        {
            r.Request.SelectedRoles.AddRange(SplitRoles(r.SelectedRoles));
            if (r.ShowLocation) r.Request.OtherLocation = JoinLocation(r.City, r.State);
            return r.Request;
        }).ToList();
    }

    public async Task<List<InterestRequestViewModel>> GetOutgoingRequestsAsync(string userId)
    {
        var rows = await _context.Interests.AsNoTracking()
            .Where(i => i.UserId == userId)
            .OrderByDescending(i => i.CreatedAt)
            .Select(i => new
            {
                Request = new InterestRequestViewModel
                {
                    Id = i.Id,
                    IdeaId = i.IdeaId,
                    IdeaTitle = i.Idea.Title,
                    InterestType = i.InterestType,
                    Status = i.Status,
                    ProposedInvestmentAmount = i.ProposedInvestmentAmount,
                    Message = i.Message,
                    CreatedAt = i.CreatedAt,
                    ResponseNote = i.ResponseNote,
                    RespondedAt = i.RespondedAt,
                    OtherUserId = i.Idea.SubmitterUserId,
                    OtherName = i.Idea.Submitter.FullName,
                    OtherPhotoUrl = i.Idea.Submitter.Profile != null ? i.Idea.Submitter.Profile.ProfilePhotoUrl : null,
                    OtherVerified = i.Idea.Submitter.Profile != null && i.Idea.Submitter.Profile.IsVerifiedFounder
                },
                i.SelectedRoles
            })
            .ToListAsync();

        return rows.Select(r =>
        {
            r.Request.SelectedRoles.AddRange(SplitRoles(r.SelectedRoles));
            return r.Request;
        }).ToList();
    }

    private static IEnumerable<string> SplitRoles(string? roles) =>
        string.IsNullOrWhiteSpace(roles) ? Enumerable.Empty<string>() : roles.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static string? JoinLocation(string? city, string? state)
    {
        var loc = string.Join(", ", new[] { city, state }.Where(v => !string.IsNullOrWhiteSpace(v)));
        return loc.Length == 0 ? null : loc;
    }

    private static string? NormaliseNote(string? note)
    {
        note = note?.Trim();
        if (string.IsNullOrEmpty(note)) return null;
        return note.Length > Interest.MaxResponseNoteLength ? note[..Interest.MaxResponseNoteLength] : note;
    }

    public async Task<ServiceResult> AcceptInterestAsync(int interestId, string userId, string? note = null)
    {
        var interest = await _context.Interests
            .Include(i => i.Idea)
                .ThenInclude(idea => idea.Team)
            .Include(i => i.User)
            .FirstOrDefaultAsync(i => i.Id == interestId && i.Idea.SubmitterUserId == userId);

        if (interest == null) return ServiceResult.Fail("Request not found.");
        if (interest.Status != InterestStatus.Pending) return ServiceResult.Fail("This request has already been answered.");

        note = NormaliseNote(note);
        interest.Status = InterestStatus.Accepted;
        interest.ResponseNote = note;
        interest.RespondedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        string? workspaceLink = null;
        if (interest.InterestType == InterestType.Work || interest.InterestType == InterestType.Both)
        {
            // Creates the team on first acceptance, always with the founder as a "Founder" member.
            var team = await _teams.EnsureTeamForIdeaAsync(interest.Idea);
            await _teams.AddMemberAsync(team, interest.UserId, interest.SelectedRoles ?? "Member");
            workspaceLink = $"/Workspace/Team/{team.Id}";
        }

        var ownerName = await _context.Users.Where(u => u.Id == userId).Select(u => u.FullName).FirstOrDefaultAsync() ?? "The founder";
        var message = workspaceLink != null
            ? $"{ownerName} accepted your request for '{interest.Idea.Title}' — welcome to the team workspace!"
            : $"{ownerName} accepted your request for '{interest.Idea.Title}'.";
        if (note != null) message += $" Their note: “{note}”";
        await _notifications.CreateAsync(interest.UserId, "Request Accepted!", message,
            workspaceLink ?? $"/Ideas/Detail/{interest.IdeaId}", category: NotificationCategory.Interest);
        return ServiceResult.Ok($"You accepted {interest.User.FullName}'s request{(workspaceLink != null ? " and added them to the team workspace" : "")}.");
    }

    public async Task<ServiceResult> RejectInterestAsync(int interestId, string userId, string? note = null)
    {
        var interest = await _context.Interests
            .Include(i => i.Idea)
            .Include(i => i.User)
            .FirstOrDefaultAsync(i => i.Id == interestId && i.Idea.SubmitterUserId == userId);

        if (interest == null) return ServiceResult.Fail("Request not found.");
        if (interest.Status != InterestStatus.Pending) return ServiceResult.Fail("This request has already been answered.");

        note = NormaliseNote(note);
        interest.Status = InterestStatus.Rejected;
        interest.ResponseNote = note;
        interest.RespondedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        var message = $"Your request for '{interest.Idea.Title}' was declined.";
        if (note != null) message += $" The founder's note: “{note}”";
        await _notifications.CreateAsync(interest.UserId, "Request Update", message, "/Interests/Manage", category: NotificationCategory.Interest);
        return ServiceResult.Ok($"You declined {interest.User.FullName}'s request.");
    }

    public async Task<bool> CancelInterestAsync(int interestId, string userId)
    {
        var interest = await _context.Interests
            .Include(i => i.Idea)
            .FirstOrDefaultAsync(i => i.Id == interestId && i.UserId == userId);

        if (interest == null || interest.Status != InterestStatus.Pending) return false;

        interest.Status = InterestStatus.Cancelled;
        await _context.SaveChangesAsync();
        await _notifications.CreateAsync(interest.Idea.SubmitterUserId, "Request Cancelled", $"A collaboration request for '{interest.Idea.Title}' was cancelled by the sender.", "/Interests/Manage", category: NotificationCategory.Interest);
        return true;
    }
}
