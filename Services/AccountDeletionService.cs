using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using StartupConnect.Data;
using StartupConnect.Models;

namespace StartupConnect.Services;

public sealed record AccountDeletionResult(bool Succeeded, string? Error = null)
{
    public static AccountDeletionResult Ok() => new(true);
    public static AccountDeletionResult Fail(string error) => new(false, error);
}

public interface IAccountDeletionService
{
    /// <summary>
    /// Permanently deletes the user and everything that depends on them, in one transaction.
    /// Nothing is changed if any step fails.
    /// </summary>
    Task<AccountDeletionResult> DeleteAccountAsync(ApplicationUser user, CancellationToken cancellationToken = default);
}

/// <summary>
/// Removes a user's data explicitly (many FKs are Restrict/NoAction to avoid SQL Server multiple
/// cascade paths). Ideas the user submitted are deleted with all their activity; the user's own
/// activity on other people's ideas (likes, comments, saves, reports, interests, team membership,
/// team messages, verification requests, connections) is deleted; anonymous traces (idea views, history editor,
/// milestone assignee/creator, moderation reviewer) are anonymised.
/// NOTE: when adding a model that references ApplicationUser or Idea, extend this service.
/// </summary>
public sealed class AccountDeletionService : IAccountDeletionService
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<AccountDeletionService> _logger;

    public AccountDeletionService(ApplicationDbContext db, UserManager<ApplicationUser> userManager, ILogger<AccountDeletionService> logger)
    {
        _db = db;
        _userManager = userManager;
        _logger = logger;
    }

    public async Task<AccountDeletionResult> DeleteAccountAsync(ApplicationUser user, CancellationToken ct = default)
    {
        var userId = user.Id;

        if (await _userManager.IsInRoleAsync(user, "Admin"))
        {
            var admins = await _userManager.GetUsersInRoleAsync("Admin");
            if (admins.Count <= 1)
                return AccountDeletionResult.Fail("You are the only administrator. Promote another admin before deleting this account.");
        }

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            // ---- Ideas the user submitted, and everything hanging off them ----
            var ideaIds = await _db.Ideas.Where(i => i.SubmitterUserId == userId).Select(i => i.Id).ToListAsync(ct);
            if (ideaIds.Count > 0)
            {
                var teamIds = await _db.Teams.Where(t => ideaIds.Contains(t.IdeaId)).Select(t => t.Id).ToListAsync(ct);
                await _db.TeamMessages.Where(m => teamIds.Contains(m.TeamId)).ExecuteDeleteAsync(ct);
                await _db.TeamMembers.Where(m => teamIds.Contains(m.TeamId)).ExecuteDeleteAsync(ct);
                await _db.Teams.Where(t => teamIds.Contains(t.Id)).ExecuteDeleteAsync(ct);

                await _db.Interests.Where(x => ideaIds.Contains(x.IdeaId)).ExecuteDeleteAsync(ct);
                await _db.IdeaLikes.Where(x => ideaIds.Contains(x.IdeaId)).ExecuteDeleteAsync(ct);
                await _db.IdeaComments.Where(x => ideaIds.Contains(x.IdeaId)).ExecuteDeleteAsync(ct);
                await _db.IdeaReports.Where(x => ideaIds.Contains(x.IdeaId)).ExecuteDeleteAsync(ct);
                await _db.IdeaViews.Where(x => ideaIds.Contains(x.IdeaId)).ExecuteDeleteAsync(ct);
                await _db.SavedIdeas.Where(x => ideaIds.Contains(x.IdeaId)).ExecuteDeleteAsync(ct);
                await _db.ChallengeSubmissions.Where(x => ideaIds.Contains(x.IdeaId)).ExecuteDeleteAsync(ct);
                await _db.IdeaHistories.Where(x => ideaIds.Contains(x.IdeaId)).ExecuteDeleteAsync(ct);
                await _db.IdeaMilestones.Where(x => ideaIds.Contains(x.IdeaId)).ExecuteDeleteAsync(ct);
                await _db.ReviewNotes.Where(x => ideaIds.Contains(x.IdeaId)).ExecuteDeleteAsync(ct);
                await _db.IdeaAnalyses.Where(x => ideaIds.Contains(x.IdeaId)).ExecuteDeleteAsync(ct);
                await _db.IdeaRolesNeeded.Where(x => ideaIds.Contains(x.IdeaId)).ExecuteDeleteAsync(ct);
                await _db.Ideas.Where(i => ideaIds.Contains(i.Id)).ExecuteDeleteAsync(ct);
            }

            // ---- The user's activity elsewhere ----
            await _db.Interests.Where(x => x.UserId == userId).ExecuteDeleteAsync(ct);
            await _db.IdeaLikes.Where(x => x.UserId == userId).ExecuteDeleteAsync(ct);
            await _db.IdeaComments.Where(x => x.UserId == userId).ExecuteDeleteAsync(ct);
            await _db.IdeaReports.Where(x => x.UserId == userId).ExecuteDeleteAsync(ct);
            await _db.SavedIdeas.Where(x => x.UserId == userId).ExecuteDeleteAsync(ct);
            await _db.TeamMessages.Where(x => x.SenderUserId == userId).ExecuteDeleteAsync(ct);
            await _db.TeamMembers.Where(x => x.UserId == userId).ExecuteDeleteAsync(ct);
            await _db.Connections.Where(c => c.RequesterId == userId || c.AddresseeId == userId).ExecuteDeleteAsync(ct);
            await _db.IdeaViews.Where(x => x.UserId == userId).ExecuteUpdateAsync(s => s.SetProperty(v => v.UserId, (string?)null), ct);
            await _db.IdeaHistories.Where(x => x.EditorId == userId).ExecuteUpdateAsync(s => s.SetProperty(h => h.EditorId, (string?)null), ct);
            // Milestones on other teams' ideas stay; they just lose this assignee/creator.
            await _db.IdeaMilestones.Where(x => x.AssigneeUserId == userId).ExecuteUpdateAsync(s => s.SetProperty(m => m.AssigneeUserId, (string?)null), ct);
            await _db.IdeaMilestones.Where(x => x.CreatedByUserId == userId).ExecuteUpdateAsync(s => s.SetProperty(m => m.CreatedByUserId, (string?)null), ct);
            // Moderation records keep their outcome but forget the (admin) reviewer.
            await _db.IdeaReports.Where(x => x.ResolvedByUserId == userId).ExecuteUpdateAsync(s => s.SetProperty(r => r.ResolvedByUserId, (string?)null), ct);
            await _db.FounderVerificationRequests.Where(x => x.ReviewedByUserId == userId).ExecuteUpdateAsync(s => s.SetProperty(r => r.ReviewedByUserId, (string?)null), ct);
            await _db.FounderVerificationRequests.Where(x => x.UserId == userId).ExecuteDeleteAsync(ct);

            var ticketIds = await _db.SupportTickets.Where(t => t.UserId == userId).Select(t => t.Id).ToListAsync(ct);
            await _db.SupportTicketMessages.Where(m => m.UserId == userId || ticketIds.Contains(m.SupportTicketId)).ExecuteDeleteAsync(ct);
            await _db.SupportTickets.Where(t => t.UserId == userId).ExecuteDeleteAsync(ct);

            await _db.Notifications.Where(n => n.UserId == userId).ExecuteDeleteAsync(ct);
            await _db.UserActivities.Where(a => a.UserId == userId).ExecuteDeleteAsync(ct);
            await _db.CoFounderMatchInsights.Where(c => c.UserId1 == userId || c.UserId2 == userId).ExecuteDeleteAsync(ct);

            var profileIds = await _db.UserProfiles.Where(p => p.UserId == userId).Select(p => p.Id).ToListAsync(ct);
            await _db.UserInterestTags.Where(t => profileIds.Contains(t.UserProfileId)).ExecuteDeleteAsync(ct);
            await _db.UserSkills.Where(s => profileIds.Contains(s.UserProfileId)).ExecuteDeleteAsync(ct);
            await _db.UserProfiles.Where(p => p.UserId == userId).ExecuteDeleteAsync(ct);
            await _db.UserSettings.Where(s => s.UserId == userId).ExecuteDeleteAsync(ct);

            // Detach anything the context may still track for this user before Identity deletes it.
            foreach (var entry in _db.ChangeTracker.Entries().Where(e => e.Entity is not ApplicationUser).ToList())
                entry.State = EntityState.Detached;

            var result = await _userManager.DeleteAsync(user);
            if (!result.Succeeded)
            {
                await tx.RollbackAsync(ct);
                var errors = string.Join(" ", result.Errors.Select(e => e.Description));
                _logger.LogWarning("Identity refused to delete user {UserId}: {Errors}", userId, errors);
                return AccountDeletionResult.Fail("We couldn't delete your account right now. Please try again or contact support.");
            }

            await tx.CommitAsync(ct);
            _logger.LogInformation("Deleted account {UserId} and {IdeaCount} idea(s)", userId, ideaIds.Count);
            return AccountDeletionResult.Ok();
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync(ct);
            _logger.LogError(ex, "Failed to delete account {UserId}", userId);
            return AccountDeletionResult.Fail("We couldn't delete your account right now. Please try again or contact support.");
        }
    }
}
