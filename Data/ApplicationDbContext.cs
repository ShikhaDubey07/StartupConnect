using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using StartupConnect.Models;

namespace StartupConnect.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<UserInterestTag> UserInterestTags => Set<UserInterestTag>();
    public DbSet<UserSkill> UserSkills => Set<UserSkill>();
    public DbSet<Idea> Ideas => Set<Idea>();
    public DbSet<IdeaRoleNeeded> IdeaRolesNeeded => Set<IdeaRoleNeeded>();
    public DbSet<Interest> Interests => Set<Interest>();
    public DbSet<Team> Teams => Set<Team>();
    public DbSet<TeamMember> TeamMembers => Set<TeamMember>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<ReviewNote> ReviewNotes => Set<ReviewNote>();
    public DbSet<ContactMessage> ContactMessages => Set<ContactMessage>();
    public DbSet<IdeaLike> IdeaLikes => Set<IdeaLike>();
    public DbSet<IdeaComment> IdeaComments => Set<IdeaComment>();
    public DbSet<IdeaReport> IdeaReports => Set<IdeaReport>();
    public DbSet<IdeaView> IdeaViews => Set<IdeaView>();
    public DbSet<SavedIdea> SavedIdeas => Set<SavedIdea>();
    public DbSet<IdeaAnalysis> IdeaAnalyses => Set<IdeaAnalysis>();
    public DbSet<UserSettings> UserSettings => Set<UserSettings>();
    public DbSet<SupportTicket> SupportTickets => Set<SupportTicket>();
    public DbSet<SupportTicketMessage> SupportTicketMessages => Set<SupportTicketMessage>();
    public DbSet<CoFounderMatchInsight> CoFounderMatchInsights => Set<CoFounderMatchInsight>();
    public DbSet<IdeaHistory> IdeaHistories => Set<IdeaHistory>();
    public DbSet<IdeaMilestone> IdeaMilestones => Set<IdeaMilestone>();
    public DbSet<StartupChallenge> StartupChallenges => Set<StartupChallenge>();
    public DbSet<ChallengeSubmission> ChallengeSubmissions => Set<ChallengeSubmission>();
    public DbSet<TeamMessage> TeamMessages => Set<TeamMessage>();
    public DbSet<UserActivity> UserActivities => Set<UserActivity>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<ApplicationUser>()
            .HasOne(a => a.Profile)
            .WithOne(p => p.User)
            .HasForeignKey<UserProfile>(p => p.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<ApplicationUser>()
            .HasOne(a => a.Settings)
            .WithOne(s => s.User)
            .HasForeignKey<UserSettings>(s => s.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<Idea>()
            .HasOne(i => i.Submitter)
            .WithMany(u => u.SubmittedIdeas)
            .HasForeignKey(i => i.SubmitterUserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<Idea>()
            .HasOne(i => i.Analysis)
            .WithOne(a => a.Idea)
            .HasForeignKey<IdeaAnalysis>(a => a.IdeaId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<IdeaHistory>()
            .HasOne(h => h.Idea)
            .WithMany(i => i.History)
            .HasForeignKey(h => h.IdeaId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<IdeaHistory>()
            .HasOne(h => h.Category)
            .WithMany()
            .HasForeignKey(h => h.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<IdeaHistory>()
            .HasOne(h => h.Editor)
            .WithMany()
            .HasForeignKey(h => h.EditorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Idea>()
            .Property(i => i.MinimumFundRequired)
            .HasPrecision(18, 2);

        builder.Entity<IdeaHistory>()
            .Property(h => h.MinimumFundRequired)
            .HasPrecision(18, 2);

        builder.Entity<Interest>()
            .Property(i => i.ProposedInvestmentAmount)
            .HasPrecision(18, 2);

        builder.Entity<Team>()
            .Property(t => t.TotalPledgedAmount)
            .HasPrecision(18, 2);

        builder.Entity<Idea>()
            .HasOne(i => i.Team)
            .WithOne(t => t.Idea)
            .HasForeignKey<Team>(t => t.IdeaId);

        builder.Entity<Interest>()
            .HasIndex(i => new { i.IdeaId, i.UserId })
            .IsUnique();

        // SQL Server disallows multiple CASCADE paths to the same table.
        // Path 1: Interests → AspNetUsers (CASCADE via UserId)
        // Path 2: Interests → Ideas (CASCADE via IdeaId) → AspNetUsers (CASCADE via SubmitterUserId)
        // Fix: restrict cascade on Interests → Ideas; delete interests manually before deleting ideas.
        builder.Entity<Interest>()
            .HasOne(i => i.Idea)
            .WithMany(idea => idea.Interests)
            .HasForeignKey(i => i.IdeaId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Interest>()
            .HasOne(i => i.User)
            .WithMany(u => u.Interests)
            .HasForeignKey(i => i.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Social features cascade configurations
        builder.Entity<IdeaLike>()
            .HasOne(l => l.Idea)
            .WithMany(i => i.Likes)
            .HasForeignKey(l => l.IdeaId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<IdeaLike>()
            .HasOne(l => l.User)
            .WithMany(u => u.IdeaLikes)
            .HasForeignKey(l => l.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<IdeaComment>()
            .HasOne(c => c.Idea)
            .WithMany(i => i.Comments)
            .HasForeignKey(c => c.IdeaId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<IdeaComment>()
            .HasOne(c => c.User)
            .WithMany(u => u.IdeaComments)
            .HasForeignKey(c => c.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<IdeaReport>()
            .HasOne(r => r.Idea)
            .WithMany(i => i.Reports)
            .HasForeignKey(r => r.IdeaId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<IdeaReport>()
            .HasOne(r => r.User)
            .WithMany(u => u.IdeaReports)
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Idea View
        builder.Entity<IdeaView>()
            .HasOne(v => v.Idea)
            .WithMany(i => i.Views)
            .HasForeignKey(v => v.IdeaId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<IdeaView>()
            .HasOne(v => v.User)
            .WithMany()
            .HasForeignKey(v => v.UserId)
            .OnDelete(DeleteBehavior.SetNull);

        // Saved Ideas
        builder.Entity<SavedIdea>()
            .HasOne(s => s.Idea)
            .WithMany(i => i.SavedByUsers)
            .HasForeignKey(s => s.IdeaId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<SavedIdea>()
            .HasOne(s => s.User)
            .WithMany(u => u.SavedIdeas)
            .HasForeignKey(s => s.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Same multiple cascade path issue for TeamMembers:
        // TeamMembers → AspNetUsers (CASCADE) AND TeamMembers → Teams → Ideas → AspNetUsers (CASCADE chain)
        builder.Entity<TeamMember>()
            .HasOne(tm => tm.Team)
            .WithMany(t => t.Members)
            .HasForeignKey(tm => tm.TeamId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<TeamMember>()
            .HasOne(tm => tm.User)
            .WithMany()
            .HasForeignKey(tm => tm.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Support Tickets
        builder.Entity<SupportTicket>()
            .HasOne(t => t.User)
            .WithMany(u => u.SupportTickets)
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<SupportTicketMessage>()
            .HasOne(m => m.Ticket)
            .WithMany(t => t.Messages)
            .HasForeignKey(m => m.SupportTicketId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<SupportTicketMessage>()
            .HasOne(m => m.User)
            .WithMany()
            .HasForeignKey(m => m.UserId)
            .OnDelete(DeleteBehavior.Restrict); // Prevent multiple cascade paths

        // AI co-founder match insight cache — unique per pair (order-independent via sorted keys)
        builder.Entity<CoFounderMatchInsight>()
            .HasIndex(c => new { c.UserId1, c.UserId2 })
            .IsUnique();

        // Advanced features cascading
        builder.Entity<IdeaMilestone>()
            .HasOne(m => m.Idea)
            .WithMany()
            .HasForeignKey(m => m.IdeaId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<ChallengeSubmission>()
            .HasOne(s => s.Challenge)
            .WithMany(c => c.Submissions)
            .HasForeignKey(s => s.ChallengeId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<ChallengeSubmission>()
            .HasOne(s => s.Idea)
            .WithMany()
            .HasForeignKey(s => s.IdeaId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<TeamMessage>()
            .HasOne(m => m.Team)
            .WithMany()
            .HasForeignKey(m => m.TeamId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<TeamMessage>()
            .HasOne(m => m.Sender)
            .WithMany()
            .HasForeignKey(m => m.SenderUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<UserActivity>()
            .HasOne(a => a.User)
            .WithMany()
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
