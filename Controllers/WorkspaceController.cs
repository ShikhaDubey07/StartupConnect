using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StartupConnect.Data;
using StartupConnect.Infrastructure;
using StartupConnect.Models;
using StartupConnect.Services;
using StartupConnect.ViewModels;

namespace StartupConnect.Controllers;

[Authorize]
public class WorkspaceController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly ITeamService _teams;

    public WorkspaceController(ApplicationDbContext context, ITeamService teams)
    {
        _context = context;
        _teams = teams;
    }

    public const int MaxMessageLength = TeamService.MaxMessageLength;

    private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    /// <summary>"My Teams": every team the user belongs to (as founder or member).</summary>
    public async Task<IActionResult> Index()
    {
        var teams = await _teams.GetTeamsForUserAsync(CurrentUserId);
        // Approved ideas the user owns that don't have a workspace yet — offer to create one.
        var ideasWithoutTeam = await _context.Ideas.AsNoTracking()
            .Where(i => i.SubmitterUserId == CurrentUserId && i.Status == IdeaStatus.Approved && i.Team == null)
            .OrderBy(i => i.Title)
            .Select(i => new { i.Id, i.Title })
            .ToListAsync();
        ViewBag.IdeasWithoutTeam = ideasWithoutTeam.Select(x => (x.Id, x.Title)).ToList();
        return View(teams);
    }

    /// <summary>Opens the idea's workspace: founders get one created on demand, members are redirected.</summary>
    public async Task<IActionResult> ForIdea(int id)
    {
        var idea = await _context.Ideas.FirstOrDefaultAsync(i => i.Id == id);
        if (idea == null) return NotFound();

        if (idea.SubmitterUserId == CurrentUserId)
        {
            if (idea.Status != IdeaStatus.Approved && !await _context.Teams.AnyAsync(t => t.IdeaId == id))
            {
                TempData["Error"] = "Your idea needs to be approved before you can open a team workspace.";
                return RedirectToAction("MyIdeas", "Ideas");
            }
            var team = await _teams.EnsureTeamForIdeaAsync(idea);
            return RedirectToAction(nameof(Team), new { id = team.Id });
        }

        var teamId = await _teams.GetTeamIdForMemberAsync(id, CurrentUserId);
        if (teamId == null) return Forbid();
        return RedirectToAction(nameof(Team), new { id = teamId });
    }

    /// <summary>The workspace page. Only team members (including the founder) may open it.</summary>
    public async Task<IActionResult> Team(int id)
    {
        var team = await _context.Teams.AsNoTracking()
            .Include(t => t.Idea).ThenInclude(i => i.Category)
            .Include(t => t.Idea).ThenInclude(i => i.RolesNeeded)
            .FirstOrDefaultAsync(t => t.Id == id);
        if (team == null) return NotFound();

        var userId = CurrentUserId;
        var isFounder = team.Idea.SubmitterUserId == userId;
        if (isFounder)
        {
            await _teams.EnsureTeamForIdeaAsync(team.Idea); // heals a missing founder membership
        }
        else if (await _teams.GetMemberAsync(id, userId) == null)
        {
            return Forbid();
        }

        var members = await _context.TeamMembers.AsNoTracking()
            .Where(m => m.TeamId == id)
            .Select(m => new WorkspaceMemberViewModel
            {
                MemberId = m.Id,
                UserId = m.UserId,
                FullName = m.User.FullName,
                Role = m.Role,
                IsFounder = m.UserId == team.Idea.SubmitterUserId,
                IsVerified = m.User.Profile != null && m.User.Profile.IsVerifiedFounder,
                PhotoUrl = m.User.Profile != null ? m.User.Profile.ProfilePhotoUrl : null,
                JoinedAt = m.JoinedAt
            })
            .ToListAsync();

        var (messages, hasMore) = await _teams.GetMessagesAsync(id, null);

        var model = new WorkspaceViewModel
        {
            TeamId = team.Id,
            TeamName = team.Name,
            Status = team.Status,
            CreatedAt = team.CreatedAt,
            IdeaId = team.IdeaId,
            IdeaTitle = team.Idea.Title,
            IdeaTagline = team.Idea.Tagline,
            IdeaCategory = team.Idea.Category.Name,
            IdeaStatus = team.Idea.Status,
            ProgressStage = team.Idea.ProgressStage,
            MinimumFundRequired = team.Idea.MinimumFundRequired,
            ExpectedTeamSize = team.Idea.ExpectedTeamSize,
            RolesNeeded = team.Idea.RolesNeeded.Select(r => r.RoleName).ToList(),
            CurrentUserId = userId,
            IsFounder = isFounder,
            Members = members.OrderByDescending(m => m.IsFounder).ThenBy(m => m.JoinedAt).ToList(),
            Roadmap = await LoadRoadmapAsync(team.IdeaId),
            Messages = messages,
            HasOlderMessages = hasMore
        };
        return View(model);
    }

    private async Task<RoadmapViewModel> LoadRoadmapAsync(int ideaId) => new()
    {
        Milestones = await _context.IdeaMilestones.AsNoTracking()
            .Where(m => m.IdeaId == ideaId)
            .OrderBy(m => m.IsCompleted)
            .ThenBy(m => m.DueDate == null)
            .ThenBy(m => m.DueDate)
            .ThenBy(m => m.CreatedAt)
            .Select(m => new MilestoneViewModel
            {
                Id = m.Id,
                Title = m.Title,
                Description = m.Description,
                DueDate = m.DueDate,
                IsCompleted = m.IsCompleted,
                CompletedAt = m.CompletedAt,
                AssigneeUserId = m.AssigneeUserId,
                AssigneeName = m.Assignee != null ? m.Assignee.FullName : null
            })
            .ToListAsync()
    };

    /// <summary>Milestone board partial, re-fetched by the page when a teammate changes something.</summary>
    [HttpGet]
    public async Task<IActionResult> Milestones(int id)
    {
        var team = await _context.Teams.AsNoTracking().Include(t => t.Idea).FirstOrDefaultAsync(t => t.Id == id);
        if (team == null) return NotFound();
        var isFounder = team.Idea.SubmitterUserId == CurrentUserId;
        if (!isFounder && await _teams.GetMemberAsync(id, CurrentUserId) == null) return Forbid();

        ViewData["TeamId"] = id;
        ViewData["IsFounder"] = isFounder;
        ViewData["IsClosed"] = team.Status == TeamStatus.Closed;
        return PartialView("_MilestoneBoard", await LoadRoadmapAsync(team.IdeaId));
    }

    // ---------------- Chat ----------------

    /// <summary>Older messages (JSON) for "load earlier messages".</summary>
    [HttpGet]
    public async Task<IActionResult> Messages(int id, int? beforeId)
    {
        if (await _teams.GetMemberAsync(id, CurrentUserId) == null) return Forbid();
        var (messages, hasMore) = await _teams.GetMessagesAsync(id, beforeId);
        return Json(new { messages, hasMore });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SendMessage(int teamId, string? content)
    {
        var (result, message) = await _teams.SendMessageAsync(teamId, CurrentUserId, content);
        if (RequireConfirmedEmailAttribute.IsAjax(Request))
        {
            if (!result.Succeeded) return BadRequest(new { success = false, message = result.Message });
            return Json(new { success = true, message = message });
        }
        if (!result.Succeeded) TempData["Error"] = result.Message;
        return RedirectToAction(nameof(Team), "Workspace", new { id = teamId }, "chat");
    }

    // ---------------- Team management ----------------

    private IActionResult BackToTeam(int teamId, ServiceResult result, string? fragment = null)
    {
        TempData[result.Succeeded ? "Success" : "Error"] = result.Message;
        return RedirectToAction(nameof(Team), "Workspace", new { id = teamId }, fragment);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeRole(int teamId, int memberId, string role) =>
        BackToTeam(teamId, await _teams.ChangeRoleAsync(teamId, memberId, role, CurrentUserId), "team");

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveMember(int teamId, int memberId) =>
        BackToTeam(teamId, await _teams.RemoveMemberAsync(teamId, memberId, CurrentUserId), "team");

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SetStatus(int teamId, TeamStatus status) =>
        BackToTeam(teamId, await _teams.SetStatusAsync(teamId, status, CurrentUserId));

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Leave(int teamId)
    {
        var result = await _teams.LeaveAsync(teamId, CurrentUserId);
        if (!result.Succeeded) return BackToTeam(teamId, result);
        TempData["Success"] = result.Message;
        return RedirectToAction(nameof(Index));
    }

    // ---------------- Milestones ----------------

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateMilestone(int teamId, MilestoneInput input) =>
        BackToTeam(teamId, await _teams.CreateMilestoneAsync(teamId, input, CurrentUserId), "milestones");

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> EditMilestone(int teamId, int milestoneId, MilestoneInput input) =>
        BackToTeam(teamId, await _teams.UpdateMilestoneAsync(teamId, milestoneId, input, CurrentUserId), "milestones");

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleMilestone(int teamId, int milestoneId, bool completed) =>
        BackToTeam(teamId, await _teams.SetMilestoneCompletedAsync(teamId, milestoneId, completed, CurrentUserId), "milestones");

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteMilestone(int teamId, int milestoneId) =>
        BackToTeam(teamId, await _teams.DeleteMilestoneAsync(teamId, milestoneId, CurrentUserId), "milestones");
}
