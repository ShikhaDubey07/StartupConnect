using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using StartupConnect.Models;

namespace StartupConnect.Data;

/// <summary>
/// Development demo data for connections and the investor dashboard (idempotent, per account/row):
/// neha@demo.in — an investor (Above ₹1L, Agriculture/Healthcare/Technology) with investment interests in
/// Rahul's ideas, a saved idea and a pending connection request to Rahul; Rahul and Priya are connected.
/// </summary>
public static class NetworkSeed
{
    public const string NehaEmail = "neha@demo.in";

    public static async Task SeedAsync(ApplicationDbContext db, UserManager<ApplicationUser> users, string demoPassword, ILogger logger)
    {
        var rahul = await users.FindByEmailAsync("rahul@demo.in");
        var priya = await users.FindByEmailAsync(CollaborationSeed.PriyaEmail);
        if (rahul == null || priya == null) return;

        var neha = await EnsureInvestorAsync(db, users, demoPassword, logger);
        if (neha == null) return;

        var now = DateTime.UtcNow;
        var rahulIdeas = await db.Ideas.Where(i => i.SubmitterUserId == rahul.Id).ToListAsync();
        var farm = rahulIdeas.FirstOrDefault(i => i.Title.StartsWith("FarmConnect"));
        var health = rahulIdeas.FirstOrDefault(i => i.Title.StartsWith("HealthBridge"));
        var edu = rahulIdeas.FirstOrDefault(i => i.Title.StartsWith("EduSpark"));

        if (farm != null && !await db.Interests.AnyAsync(i => i.IdeaId == farm.Id && i.UserId == neha.Id))
        {
            db.Interests.Add(new Interest
            {
                IdeaId = farm.Id, UserId = neha.Id, InterestType = InterestType.Invest, ProposedInvestmentAmount = 100000,
                Message = "Farmer-first marketplaces are a thesis of mine. Happy to write a ₹1L angel cheque for the pilot.",
                Status = InterestStatus.Pending, CreatedAt = now.AddDays(-2)
            });
        }
        if (health != null && !await db.Interests.AnyAsync(i => i.IdeaId == health.Id && i.UserId == neha.Id))
        {
            db.Interests.Add(new Interest
            {
                IdeaId = health.Id, UserId = neha.Id, InterestType = InterestType.Invest, ProposedInvestmentAmount = 200000,
                Message = "Congrats on the Pitch-Off win. I'd like to back the PHC pilot.",
                Status = InterestStatus.Accepted, CreatedAt = now.AddDays(-8)
            });
        }
        if (edu != null && !await db.SavedIdeas.AnyAsync(s => s.IdeaId == edu.Id && s.UserId == neha.Id))
        {
            db.SavedIdeas.Add(new SavedIdea { IdeaId = edu.Id, UserId = neha.Id, SavedAt = now.AddDays(-1) });
        }

        await EnsureConnectionAsync(db, rahul.Id, priya.Id, ConnectionStatus.Accepted,
            "Loved your CraftKart pitch — let's stay in touch.", now.AddDays(-11), now.AddDays(-10));
        await EnsureConnectionAsync(db, neha.Id, rahul.Id, ConnectionStatus.Pending,
            "Hi Rahul, I invest in agri and health startups and would love to follow FarmConnect's progress.", now.AddHours(-5), null);

        await db.SaveChangesAsync();
    }

    private static async Task EnsureConnectionAsync(ApplicationDbContext db, string requesterId, string addresseeId,
        ConnectionStatus status, string message, DateTime createdAt, DateTime? respondedAt)
    {
        var (a, b) = Connection.Order(requesterId, addresseeId);
        if (await db.Connections.AnyAsync(c => c.UserAId == a && c.UserBId == b)) return;
        var connection = new Connection
        {
            Status = status, Message = message, CreatedAt = createdAt, UpdatedAt = respondedAt ?? createdAt, RespondedAt = respondedAt
        };
        connection.SetPair(requesterId, addresseeId);
        db.Connections.Add(connection);
    }

    private static async Task<ApplicationUser?> EnsureInvestorAsync(ApplicationDbContext db, UserManager<ApplicationUser> users, string password, ILogger logger)
    {
        var neha = await users.FindByEmailAsync(NehaEmail);
        if (neha != null) return neha;

        neha = new ApplicationUser
        {
            UserName = NehaEmail, Email = NehaEmail, FullName = "Neha Kapoor", City = "Mumbai", State = "Maharashtra", Age = 38,
            EmailConfirmed = true, CreatedAt = DateTime.UtcNow.AddDays(-40)
        };
        var result = await users.CreateAsync(neha, password);
        if (!result.Succeeded)
        {
            logger.LogWarning("Could not seed demo investor {Email}: {Errors}", NehaEmail, string.Join(" ", result.Errors.Select(e => e.Description)));
            return null;
        }
        await users.AddToRoleAsync(neha, "Member");
        await users.AddToRoleAsync(neha, "Investor");

        var categoryIds = await db.Categories
            .Where(c => c.Name == "Agriculture" || c.Name == "Healthcare" || c.Name == "Technology")
            .Select(c => c.Id).ToListAsync();
        db.UserProfiles.Add(new UserProfile
        {
            UserId = neha.Id,
            Bio = "Angel investor and ex-banker. I back early founders building for rural India — agri, health and practical tech.",
            TimeAvailability = TimeAvailability.WeekendsOnly,
            HoursPerWeek = 6,
            InvestmentCapacity = InvestmentCapacity.Above1L,
            IsInvestor = true,
            LinkedInUrl = "https://www.linkedin.com/in/neha-kapoor-demo",
            ProfileCompletionPercent = 90,
            Skills = new List<UserSkill> { new() { SkillName = "Finance" } },
            InterestTags = categoryIds.Select(id => new UserInterestTag { CategoryId = id }).ToList()
        });
        await db.SaveChangesAsync();
        logger.LogInformation("Seeded demo investor {Email}", NehaEmail);
        return neha;
    }
}
