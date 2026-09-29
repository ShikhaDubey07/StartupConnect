using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using StartupConnect.Data;
using StartupConnect.Hubs;
using StartupConnect.Models;

namespace StartupConnect.Services;

public sealed record TeamMessageDto(int Id, int TeamId, string SenderId, string SenderName, string Content, DateTime SentAt)
{
    /// <summary>Values come from SQL Server without a Kind; they are UTC (serialised with a trailing Z).</summary>
    public TeamMessageDto AsUtc() => this with { SentAt = DateTime.SpecifyKind(SentAt, DateTimeKind.Utc) };
}

public sealed class MyTeamItem
{
    public int TeamId { get; init; }
    public string TeamName { get; init; } = string.Empty;
    public TeamStatus Status { get; init; }
    public int IdeaId { get; init; }
    public string IdeaTitle { get; init; } = string.Empty;
    public string IdeaTagline { get; init; } = string.Empty;
    public string MyRole { get; init; } = string.Empty;
    public bool IsFounder { get; init; }
    public List<string> MemberNames { get; init; } = new();
    public int MilestonesDone { get; init; }
    public int MilestonesTotal { get; init; }
    public TeamMessageDto? LastMessage { get; init; }
    public DateTime JoinedAt { get; init; }
}

public sealed class MilestoneInput
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime? DueDate { get; set; }
    public string? AssigneeUserId { get; set; }
}

public interface ITeamService
{
    /// <summary>Returns the idea's team, creating it (with the founder as a member) when missing.</summary>
    Task<Team> EnsureTeamForIdeaAsync(Idea idea);
    /// <summary>Adds a member (no-op if already a member). Returns true when a row was added.</summary>
    Task<bool> AddMemberAsync(Team team, string userId, string role);
    Task<TeamMember?> GetMemberAsync(int teamId, string userId);
    Task<List<MyTeamItem>> GetTeamsForUserAsync(string userId);
    Task<int?> GetTeamIdForMemberAsync(int ideaId, string userId);

    Task<ServiceResult> ChangeRoleAsync(int teamId, int memberId, string role, string actingUserId);
    Task<ServiceResult> RemoveMemberAsync(int teamId, int memberId, string actingUserId);
    Task<ServiceResult> LeaveAsync(int teamId, string userId);
    Task<ServiceResult> SetStatusAsync(int teamId, TeamStatus status, string actingUserId);

    Task<ServiceResult> CreateMilestoneAsync(int teamId, MilestoneInput input, string actingUserId);
    Task<ServiceResult> UpdateMilestoneAsync(int teamId, int milestoneId, MilestoneInput input, string actingUserId);
    Task<ServiceResult> SetMilestoneCompletedAsync(int teamId, int milestoneId, bool completed, string actingUserId);
    Task<ServiceResult> DeleteMilestoneAsync(int teamId, int milestoneId, string actingUserId);

    Task<(List<TeamMessageDto> Messages, bool HasMore)> GetMessagesAsync(int teamId, int? beforeId, int take = 30);
    Task<(ServiceResult Result, TeamMessageDto? Message)> SendMessageAsync(int teamId, string userId, string? content);
}

/// <summary>
/// Team workspace rules:
/// - The idea owner is the team's Founder (a TeamMember row with Role "Founder"); they can't be removed or leave.
/// - Founder manages the team: member roles, removing members, team status.
/// - Founder and members create/edit/complete milestones; only the founder deletes them.
/// - A Closed team is read-only (no chat, no milestone changes) until the founder changes its status.
/// </summary>
public sealed class TeamService : ITeamService
{
    public const int MaxMessageLength = 2000;
    public const int MaxMilestoneTitle = 150;
    public const int MaxMilestoneDescription = 1000;

    private readonly ApplicationDbContext _db;
    private readonly INotificationService _notifications;
    private readonly IActivityService _activity;
    private readonly IHubContext<TeamHub> _hub;
    private readonly ILogger<TeamService> _logger;

    public TeamService(ApplicationDbContext db, INotificationService notifications, IActivityService activity,
        IHubContext<TeamHub> hub, ILogger<TeamService> logger)
    {
        _db = db;
        _notifications = notifications;
        _activity = activity;
        _hub = hub;
        _logger = logger;
    }

    public static string NormalizeRole(string? role)
    {
        var parts = (role ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase);
        var joined = string.Join(", ", parts);
        if (joined.Length == 0) joined = "Member";
        return joined.Length > TeamRoles.MaxLength ? joined[..TeamRoles.MaxLength].TrimEnd(' ', ',') : joined;
    }

    public async Task<Team> EnsureTeamForIdeaAsync(Idea idea)
    {
        var team = await _db.Teams.FirstOrDefaultAsync(t => t.IdeaId == idea.Id);
        var created = false;
        if (team == null)
        {
            team = new Team { IdeaId = idea.Id, Name = $"{idea.Title} Team" };
            _db.Teams.Add(team);
            await _db.SaveChangesAsync();
            created = true;
        }

        var founder = await _db.TeamMembers.FirstOrDefaultAsync(m => m.TeamId == team.Id && m.UserId == idea.SubmitterUserId);
        if (founder == null)
        {
            _db.TeamMembers.Add(new TeamMember { TeamId = team.Id, UserId = idea.SubmitterUserId, Role = TeamRoles.Founder });
            await _db.SaveChangesAsync();
        }
        else if (founder.Role != TeamRoles.Founder)
        {
            founder.Role = TeamRoles.Founder;
            await _db.SaveChangesAsync();
        }

        if (created)
        {
            await _activity.RecordAsync(idea.SubmitterUserId, ActivityTypes.TeamCreated,
                $"You created the team workspace for \"{idea.Title}\".", $"/Workspace/Team/{team.Id}");
        }
        return team;
    }

    public async Task<bool> AddMemberAsync(Team team, string userId, string role)
    {
        if (await _db.TeamMembers.AnyAsync(m => m.TeamId == team.Id && m.UserId == userId)) return false;

        _db.TeamMembers.Add(new TeamMember { TeamId = team.Id, UserId = userId, Role = NormalizeRole(role) });
        await _db.SaveChangesAsync();

        var ideaTitle = await _db.Ideas.Where(i => i.Id == team.IdeaId).Select(i => i.Title).FirstOrDefaultAsync() ?? team.Name;
        await _activity.RecordAsync(userId, ActivityTypes.JoinedTeam, $"You joined the team for \"{ideaTitle}\".", $"/Workspace/Team/{team.Id}");
        await BroadcastTeamChangedAsync(team.Id, "members");
        return true;
    }

    public Task<TeamMember?> GetMemberAsync(int teamId, string userId) =>
        _db.TeamMembers.FirstOrDefaultAsync(m => m.TeamId == teamId && m.UserId == userId);

    public Task<int?> GetTeamIdForMemberAsync(int ideaId, string userId) =>
        _db.TeamMembers.Where(m => m.Team.IdeaId == ideaId && m.UserId == userId)
            .Select(m => (int?)m.TeamId).FirstOrDefaultAsync();

    public async Task<List<MyTeamItem>> GetTeamsForUserAsync(string userId)
    {
        var teams = await _db.TeamMembers.AsNoTracking()
            .Where(m => m.UserId == userId)
            .Select(m => new
            {
                m.TeamId,
                m.Role,
                m.JoinedAt,
                m.Team.Name,
                m.Team.Status,
                m.Team.IdeaId,
                IdeaTitle = m.Team.Idea.Title,
                IdeaTagline = m.Team.Idea.Tagline,
                FounderId = m.Team.Idea.SubmitterUserId,
                Members = m.Team.Members.OrderBy(x => x.JoinedAt).Select(x => x.User.FullName).ToList(),
                Done = m.Team.Idea.Milestones.Count(x => x.IsCompleted),
                Total = m.Team.Idea.Milestones.Count()
            })
            .ToListAsync();

        var teamIds = teams.Select(t => t.TeamId).ToList();
        var lastMessageIds = await _db.TeamMessages
            .Where(x => teamIds.Contains(x.TeamId))
            .GroupBy(x => x.TeamId)
            .Select(g => g.Max(x => x.Id))
            .ToListAsync();
        var lastMessages = await _db.TeamMessages.AsNoTracking()
            .Where(x => lastMessageIds.Contains(x.Id))
            .Select(x => new TeamMessageDto(x.Id, x.TeamId, x.SenderUserId, x.Sender.FullName, x.Content, x.SentAt))
            .ToListAsync();
        var lastByTeam = lastMessages.Select(m => m.AsUtc()).ToDictionary(x => x.TeamId);

        return teams
            .Select(t => new MyTeamItem
            {
                TeamId = t.TeamId,
                TeamName = t.Name,
                Status = t.Status,
                IdeaId = t.IdeaId,
                IdeaTitle = t.IdeaTitle,
                IdeaTagline = t.IdeaTagline,
                MyRole = t.Role,
                IsFounder = t.FounderId == userId,
                MemberNames = t.Members,
                MilestonesDone = t.Done,
                MilestonesTotal = t.Total,
                LastMessage = lastByTeam.GetValueOrDefault(t.TeamId),
                JoinedAt = t.JoinedAt
            })
            .OrderByDescending(t => t.LastMessage?.SentAt ?? t.JoinedAt)
            .ToList();
    }

    // ---------------- Team management ----------------

    private async Task<(Team? Team, bool IsFounder, bool IsMember)> LoadTeamAsync(int teamId, string userId)
    {
        var team = await _db.Teams.Include(t => t.Idea).FirstOrDefaultAsync(t => t.Id == teamId);
        if (team == null) return (null, false, false);
        var isFounder = team.Idea.SubmitterUserId == userId;
        var isMember = isFounder || await _db.TeamMembers.AnyAsync(m => m.TeamId == teamId && m.UserId == userId);
        return (team, isFounder, isMember);
    }

    public async Task<ServiceResult> ChangeRoleAsync(int teamId, int memberId, string role, string actingUserId)
    {
        var (team, isFounder, _) = await LoadTeamAsync(teamId, actingUserId);
        if (team == null || !isFounder) return ServiceResult.Fail("Only the founder can change member roles.");

        var member = await _db.TeamMembers.FirstOrDefaultAsync(m => m.Id == memberId && m.TeamId == teamId);
        if (member == null) return ServiceResult.Fail("That member is no longer on the team.");
        if (member.UserId == team.Idea.SubmitterUserId) return ServiceResult.Fail("The founder's role can't be changed.");

        role = (role ?? string.Empty).Trim();
        if (role.Length == 0) return ServiceResult.Fail("Please enter a role.");
        if (role.Length > TeamRoles.MaxLength) return ServiceResult.Fail($"Roles can be at most {TeamRoles.MaxLength} characters.");
        if (string.Equals(role, TeamRoles.Founder, StringComparison.OrdinalIgnoreCase))
            return ServiceResult.Fail("Only the idea owner can hold the Founder role — try \"Co-founder\".");

        member.Role = role;
        await _db.SaveChangesAsync();
        await _notifications.CreateAsync(member.UserId, "Your team role changed",
            $"You're now \"{role}\" on the {team.Name}.", $"/Workspace/Team/{teamId}", category: NotificationCategory.Team);
        await BroadcastTeamChangedAsync(teamId, "members");
        return ServiceResult.Ok("Role updated.");
    }

    public async Task<ServiceResult> RemoveMemberAsync(int teamId, int memberId, string actingUserId)
    {
        var (team, isFounder, _) = await LoadTeamAsync(teamId, actingUserId);
        if (team == null || !isFounder) return ServiceResult.Fail("Only the founder can remove members.");

        var member = await _db.TeamMembers.Include(m => m.User).FirstOrDefaultAsync(m => m.Id == memberId && m.TeamId == teamId);
        if (member == null) return ServiceResult.Fail("That member is no longer on the team.");
        if (member.UserId == team.Idea.SubmitterUserId) return ServiceResult.Fail("The founder can't be removed from the team.");

        await UnassignMilestonesAsync(team.IdeaId, member.UserId);
        _db.TeamMembers.Remove(member);
        await _db.SaveChangesAsync();

        await _notifications.CreateAsync(member.UserId, "Removed from team",
            $"You're no longer a member of the {team.Name}.", "/Workspace", category: NotificationCategory.Team);
        await _hub.Clients.User(member.UserId).SendAsync(TeamHub.TeamChangedEvent, new { teamId, what = "removed" });
        await BroadcastTeamChangedAsync(teamId, "members");
        return ServiceResult.Ok($"{member.User.FullName} was removed from the team.");
    }

    public async Task<ServiceResult> LeaveAsync(int teamId, string userId)
    {
        var (team, isFounder, isMember) = await LoadTeamAsync(teamId, userId);
        if (team == null || !isMember) return ServiceResult.Fail("You're not a member of this team.");
        if (isFounder) return ServiceResult.Fail("As the founder you can't leave your own team. Set its status to Closed instead.");

        var member = await _db.TeamMembers.Include(m => m.User).FirstAsync(m => m.TeamId == teamId && m.UserId == userId);
        await UnassignMilestonesAsync(team.IdeaId, userId);
        _db.TeamMembers.Remove(member);
        await _activity.RecordAsync(userId, ActivityTypes.LeftTeam, $"You left the {team.Name}.", $"/Ideas/Detail/{team.IdeaId}", save: false);
        await _db.SaveChangesAsync();

        await _notifications.CreateAsync(team.Idea.SubmitterUserId, "A member left your team",
            $"{member.User.FullName} left the {team.Name}.", $"/Workspace/Team/{teamId}", category: NotificationCategory.Team);
        await BroadcastTeamChangedAsync(teamId, "members");
        return ServiceResult.Ok($"You left the {team.Name}.");
    }

    public async Task<ServiceResult> SetStatusAsync(int teamId, TeamStatus status, string actingUserId)
    {
        if (!Enum.IsDefined(status)) return ServiceResult.Fail("Unknown status.");
        var (team, isFounder, _) = await LoadTeamAsync(teamId, actingUserId);
        if (team == null || !isFounder) return ServiceResult.Fail("Only the founder can change the team status.");
        if (team.Status == status) return ServiceResult.Ok($"The team is already {status}.");

        team.Status = status;
        await _db.SaveChangesAsync();

        var memberIds = await MemberIdsAsync(teamId);
        foreach (var id in memberIds.Where(id => id != actingUserId))
        {
            await _notifications.CreateAsync(id, "Team status updated",
                $"The {team.Name} is now {status}.", $"/Workspace/Team/{teamId}", category: NotificationCategory.Team);
        }
        await BroadcastTeamChangedAsync(teamId, "status");
        return ServiceResult.Ok($"Team status set to {status}.");
    }

    // ---------------- Milestones ----------------

    private async Task<ServiceResult?> ValidateMilestoneInputAsync(int teamId, MilestoneInput input)
    {
        input.Title = (input.Title ?? string.Empty).Trim();
        input.Description = input.Description?.Trim();
        if (input.Title.Length == 0) return ServiceResult.Fail("Give the milestone a title.");
        if (input.Title.Length > MaxMilestoneTitle) return ServiceResult.Fail($"Milestone titles can be at most {MaxMilestoneTitle} characters.");
        if ((input.Description?.Length ?? 0) > MaxMilestoneDescription)
            return ServiceResult.Fail($"Descriptions can be at most {MaxMilestoneDescription} characters.");
        if (input.DueDate.HasValue && (input.DueDate.Value.Year < 2000 || input.DueDate.Value.Year > 2100))
            return ServiceResult.Fail("Please pick a valid due date.");
        if (!string.IsNullOrEmpty(input.AssigneeUserId)
            && !await _db.TeamMembers.AnyAsync(m => m.TeamId == teamId && m.UserId == input.AssigneeUserId))
            return ServiceResult.Fail("Milestones can only be assigned to current team members.");
        return null;
    }

    private static string ClosedMessage => "This team is closed — the founder can reopen it by changing its status.";

    public async Task<ServiceResult> CreateMilestoneAsync(int teamId, MilestoneInput input, string actingUserId)
    {
        var (team, _, isMember) = await LoadTeamAsync(teamId, actingUserId);
        if (team == null || !isMember) return ServiceResult.Fail("Only team members can add milestones.");
        if (team.Status == TeamStatus.Closed) return ServiceResult.Fail(ClosedMessage);
        var invalid = await ValidateMilestoneInputAsync(teamId, input);
        if (invalid != null) return invalid;

        var milestone = new IdeaMilestone
        {
            IdeaId = team.IdeaId,
            Title = input.Title,
            Description = input.Description ?? string.Empty,
            DueDate = input.DueDate?.Date,
            AssigneeUserId = string.IsNullOrEmpty(input.AssigneeUserId) ? null : input.AssigneeUserId,
            CreatedByUserId = actingUserId
        };
        _db.IdeaMilestones.Add(milestone);
        await _activity.RecordAsync(actingUserId, ActivityTypes.MilestoneCreated,
            $"You added the milestone \"{milestone.Title}\" to {team.Idea.Title}.", $"/Workspace/Team/{teamId}", save: false);
        await _db.SaveChangesAsync();

        await NotifyAssigneeAsync(team, milestone, actingUserId);
        await BroadcastTeamChangedAsync(teamId, "milestones");
        return ServiceResult.Ok("Milestone added.");
    }

    public async Task<ServiceResult> UpdateMilestoneAsync(int teamId, int milestoneId, MilestoneInput input, string actingUserId)
    {
        var (team, _, isMember) = await LoadTeamAsync(teamId, actingUserId);
        if (team == null || !isMember) return ServiceResult.Fail("Only team members can edit milestones.");
        if (team.Status == TeamStatus.Closed) return ServiceResult.Fail(ClosedMessage);
        var milestone = await _db.IdeaMilestones.FirstOrDefaultAsync(m => m.Id == milestoneId && m.IdeaId == team.IdeaId);
        if (milestone == null) return ServiceResult.Fail("That milestone no longer exists.");
        var invalid = await ValidateMilestoneInputAsync(teamId, input);
        if (invalid != null) return invalid;

        var previousAssignee = milestone.AssigneeUserId;
        milestone.Title = input.Title;
        milestone.Description = input.Description ?? string.Empty;
        milestone.DueDate = input.DueDate?.Date;
        milestone.AssigneeUserId = string.IsNullOrEmpty(input.AssigneeUserId) ? null : input.AssigneeUserId;
        await _db.SaveChangesAsync();

        if (milestone.AssigneeUserId != previousAssignee) await NotifyAssigneeAsync(team, milestone, actingUserId);
        await BroadcastTeamChangedAsync(teamId, "milestones");
        return ServiceResult.Ok("Milestone updated.");
    }

    public async Task<ServiceResult> SetMilestoneCompletedAsync(int teamId, int milestoneId, bool completed, string actingUserId)
    {
        var (team, _, isMember) = await LoadTeamAsync(teamId, actingUserId);
        if (team == null || !isMember) return ServiceResult.Fail("Only team members can update milestones.");
        if (team.Status == TeamStatus.Closed) return ServiceResult.Fail(ClosedMessage);
        var milestone = await _db.IdeaMilestones.FirstOrDefaultAsync(m => m.Id == milestoneId && m.IdeaId == team.IdeaId);
        if (milestone == null) return ServiceResult.Fail("That milestone no longer exists.");
        if (milestone.IsCompleted == completed) return ServiceResult.Ok(completed ? "Already completed." : "Already open.");

        milestone.IsCompleted = completed;
        milestone.CompletedAt = completed ? DateTime.UtcNow : null;
        if (completed)
        {
            await _activity.RecordAsync(actingUserId, ActivityTypes.MilestoneCompleted,
                $"You completed the milestone \"{milestone.Title}\" for {team.Idea.Title}.", $"/Workspace/Team/{teamId}", save: false);
        }
        await _db.SaveChangesAsync();

        if (completed && team.Idea.SubmitterUserId != actingUserId)
        {
            var who = await _db.Users.Where(u => u.Id == actingUserId).Select(u => u.FullName).FirstOrDefaultAsync() ?? "A teammate";
            await _notifications.CreateAsync(team.Idea.SubmitterUserId, "Milestone completed 🎯",
                $"{who} completed \"{milestone.Title}\" for {team.Idea.Title}.", $"/Workspace/Team/{teamId}", category: NotificationCategory.Team);
        }
        await BroadcastTeamChangedAsync(teamId, "milestones");
        return ServiceResult.Ok(completed ? "Milestone completed — nice work!" : "Milestone reopened.");
    }

    public async Task<ServiceResult> DeleteMilestoneAsync(int teamId, int milestoneId, string actingUserId)
    {
        var (team, isFounder, _) = await LoadTeamAsync(teamId, actingUserId);
        if (team == null || !isFounder) return ServiceResult.Fail("Only the founder can delete milestones.");
        var milestone = await _db.IdeaMilestones.FirstOrDefaultAsync(m => m.Id == milestoneId && m.IdeaId == team.IdeaId);
        if (milestone == null) return ServiceResult.Fail("That milestone no longer exists.");

        _db.IdeaMilestones.Remove(milestone);
        await _db.SaveChangesAsync();
        await BroadcastTeamChangedAsync(teamId, "milestones");
        return ServiceResult.Ok("Milestone deleted.");
    }

    private async Task NotifyAssigneeAsync(Team team, IdeaMilestone milestone, string actingUserId)
    {
        if (string.IsNullOrEmpty(milestone.AssigneeUserId) || milestone.AssigneeUserId == actingUserId) return;
        await _notifications.CreateAsync(milestone.AssigneeUserId, "New milestone assigned",
            $"You were assigned \"{milestone.Title}\" in the {team.Name}.", $"/Workspace/Team/{team.Id}", category: NotificationCategory.Team);
    }

    private Task UnassignMilestonesAsync(int ideaId, string userId) =>
        _db.IdeaMilestones.Where(m => m.IdeaId == ideaId && m.AssigneeUserId == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(m => m.AssigneeUserId, (string?)null));

    // ---------------- Chat ----------------

    public async Task<(List<TeamMessageDto> Messages, bool HasMore)> GetMessagesAsync(int teamId, int? beforeId, int take = 30)
    {
        take = Math.Clamp(take, 1, 100);
        var query = _db.TeamMessages.AsNoTracking().Where(m => m.TeamId == teamId);
        if (beforeId.HasValue) query = query.Where(m => m.Id < beforeId.Value);

        var page = await query
            .OrderByDescending(m => m.Id)
            .Take(take + 1)
            .Select(m => new TeamMessageDto(m.Id, m.TeamId, m.SenderUserId, m.Sender.FullName, m.Content, m.SentAt))
            .ToListAsync();

        var hasMore = page.Count > take;
        var messages = page.Take(take).OrderBy(m => m.Id).Select(m => m.AsUtc()).ToList(); // oldest first → newest at the bottom
        return (messages, hasMore);
    }

    public async Task<(ServiceResult Result, TeamMessageDto? Message)> SendMessageAsync(int teamId, string userId, string? content)
    {
        var (team, _, isMember) = await LoadTeamAsync(teamId, userId);
        if (team == null || !isMember) return (ServiceResult.Fail("You're not a member of this team."), null);
        if (team.Status == TeamStatus.Closed) return (ServiceResult.Fail(ClosedMessage), null);

        content = content?.Trim() ?? string.Empty;
        if (content.Length == 0) return (ServiceResult.Fail("Your message is empty."), null);
        if (content.Length > MaxMessageLength) return (ServiceResult.Fail($"Messages can be at most {MaxMessageLength} characters."), null);

        var message = new TeamMessage { TeamId = teamId, SenderUserId = userId, Content = content, SentAt = DateTime.UtcNow };
        _db.TeamMessages.Add(message);
        await _db.SaveChangesAsync();

        var senderName = await _db.Users.Where(u => u.Id == userId).Select(u => u.FullName).FirstAsync();
        var dto = new TeamMessageDto(message.Id, teamId, userId, senderName, content, message.SentAt).AsUtc();

        try
        {
            var memberIds = await MemberIdsAsync(teamId);
            await _hub.Clients.Users(memberIds).SendAsync(TeamHub.MessageEvent, dto);
        }
        catch (Exception ex)
        {
            // The message is saved; clients will see it on refresh even if the push failed.
            _logger.LogWarning(ex, "Could not push team message {MessageId}", message.Id);
        }
        return (ServiceResult.Ok("Sent."), dto);
    }

    private Task<List<string>> MemberIdsAsync(int teamId) =>
        _db.TeamMembers.Where(m => m.TeamId == teamId).Select(m => m.UserId).ToListAsync();

    private async Task BroadcastTeamChangedAsync(int teamId, string what)
    {
        try
        {
            var memberIds = await MemberIdsAsync(teamId);
            await _hub.Clients.Users(memberIds).SendAsync(TeamHub.TeamChangedEvent, new { teamId, what });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not push team change for team {TeamId}", teamId);
        }
    }
}
