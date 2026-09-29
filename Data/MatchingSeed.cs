using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using StartupConnect.Models;
using StartupConnect.Services;

namespace StartupConnect.Data;

/// <summary>
/// Development demo data for matching & discovery (idempotent, per account/row). Adds members that make
/// complementarity visible for Rahul (developer/designer, Bangalore):
/// ananya@demo.in (Marketing + Sales, Bangalore — covers what Rahul's ideas need),
/// vikram@demo.in (Developer + Designer, Bangalore — a "clone" of Rahul that used to top the old scorer),
/// karan@demo.in (Operations + Finance, Mysuru), sana@demo.in (Medical Advisor, Hyderabad),
/// meera@demo.in (Content + Marketing, Pune, weekends) and rohan@demo.in (Sales, <b>Private</b> — never listed).
/// Each has an approved idea overlapping one of Rahul's (similar-ideas demo), plus recent likes/interests.
/// All use the demo password.
/// </summary>
public static class MatchingSeed
{
    private sealed record DemoMember(string Email, string Name, string City, string State, int Age, string Bio,
        string[] Skills, string[] Interests, TimeAvailability Availability, int Hours, bool Verified = false, bool Private = false);

    private static readonly DemoMember[] Members =
    [
        new("ananya@demo.in", "Ananya Iyer", "Bangalore", "Karnataka", 27,
            "Growth marketer (ex-Meesho) who has taken two rural-commerce apps from 0 to 50k users. Loves D2C and agri supply chains.",
            ["Marketing", "Sales"], ["Agriculture", "Education"], TimeAvailability.FullTime, 40, Verified: true),
        new("vikram@demo.in", "Vikram Rao", "Bangalore", "Karnataka", 25,
            "Full-stack developer and UI designer. React, Node and Figma. Looking to build a dev-tools startup.",
            ["Developer", "Designer"], ["Technology"], TimeAvailability.FullTime, 40),
        new("karan@demo.in", "Karan Singh", "Mysuru", "Karnataka", 31,
            "Ran cold-chain operations for a dairy co-op for six years; CA dropout who still loves spreadsheets.",
            ["Operations", "Finance"], ["Agriculture"], TimeAvailability.PartTime, 20),
        new("sana@demo.in", "Dr. Sana Khan", "Hyderabad", "Telangana", 34,
            "MBBS, public-health researcher. Ran PHC programmes across rural Telangana; want to build care for the last mile.",
            ["Medical Advisor", "Operations"], ["Healthcare", "Social Impact"], TimeAvailability.PartTime, 15),
        new("meera@demo.in", "Meera Joshi", "Pune", "Maharashtra", 23,
            "Creates Marathi and Hindi explainer videos (80k subscribers). Weekend builder, weekday teacher.",
            ["Content Creator", "Marketing"], ["Education"], TimeAvailability.WeekendsOnly, 8),
        new("rohan@demo.in", "Rohan Malhotra", "Delhi", "Delhi", 29,
            "Enterprise sales lead. Keeps a private profile while exploring.",
            ["Sales"], ["Technology"], TimeAvailability.PartTime, 10, Private: true)
    ];

    public static async Task SeedAsync(ApplicationDbContext db, UserManager<ApplicationUser> users, string demoPassword, ILogger logger)
    {
        var rahul = await users.FindByEmailAsync("rahul@demo.in");
        if (rahul == null) return;

        var categories = await db.Categories.ToListAsync();
        int? CategoryId(string name) => categories.FirstOrDefault(c => c.Name == name)?.Id;

        var created = new Dictionary<string, ApplicationUser>();
        foreach (var m in Members)
        {
            var user = await EnsureMemberAsync(db, users, logger, m, demoPassword, CategoryId);
            if (user != null) created[m.Email] = user;
        }
        if (created.Count < Members.Length) return;

        var now = DateTime.UtcNow;
        await EnsureIdeaAsync(db, created["ananya@demo.in"], "KisanMandi — Live Crop Price Alerts",
            "Mandi prices on WhatsApp so farmers sell directly to buyers at fair prices",
            "Daily crop prices from nearby mandis and verified bulk buyers, delivered in the farmer's language over WhatsApp, with one-tap listing to sell produce directly.",
            "Farmers sell to middlemen at low prices because they don't know today's mandi rates or who else would buy their produce.",
            "Price alerts plus a direct buyer marketplace with pickup logistics, cutting out intermediaries for small farmers.",
            "Small and marginal farmers in Karnataka and Maharashtra; urban bulk buyers and restaurants",
            "Commission on direct sales + paid alerts for traders", CategoryId("Agriculture"), 250000, IdeaProgressStage.MVP,
            ["Developer", "Operations"], now.AddDays(-4));
        await EnsureIdeaAsync(db, created["meera@demo.in"], "BhashaLearn — Regional Language Tutoring",
            "Short video lessons in Marathi, Hindi and Tamil for rural students",
            "Bite-sized regional-language video courses for school and college students with offline downloads and doubt-solving over voice notes.",
            "Most quality learning content is only in English, so students in Tier 2/3 towns fall behind.",
            "Localized video courses by local teachers, offline access on low-end phones and affordable monthly passes.",
            "Students aged 14-22 in Tier 2 and Tier 3 cities",
            "Freemium lessons + ₹99/month pass + school partnerships", CategoryId("Education"), 150000, IdeaProgressStage.Prototype,
            ["Developer", "Sales"], now.AddDays(-2));
        await EnsureIdeaAsync(db, created["sana@demo.in"], "CareCall — Village Health Helpline",
            "Doctor consultations over a phone call for rural patients",
            "A telemedicine helpline where ASHA workers connect village patients to qualified doctors over ordinary phone and low-bandwidth video calls, with medicine delivery.",
            "Rural patients travel hours to see a qualified doctor; local clinics lack specialists.",
            "Telemedicine over phone calls with trained local health workers, e-prescriptions and pharmacy tie-ups.",
            "Rural population and primary health centres in Telangana and Andhra Pradesh",
            "Per-consultation fee + state health mission contracts", CategoryId("Healthcare"), 600000, IdeaProgressStage.Research,
            ["Developer", "Marketing"], now.AddDays(-6));
        await EnsureIdeaAsync(db, created["vikram@demo.in"], "DevDock — Team Dev Environments",
            "One-click cloud dev environments for small software teams",
            "Spin up reproducible development environments in the browser with shared previews and secrets management for teams of 2-20 engineers.",
            "Onboarding a developer takes days of environment setup and 'works on my machine' bugs.",
            "Container-based cloud workspaces with templates per repo and instant preview links.",
            "Software startups and agencies in India",
            "Per-seat SaaS subscription", CategoryId("Technology"), 900000, IdeaProgressStage.Idea,
            ["Sales", "Marketing"], now.AddDays(-9));
        await db.SaveChangesAsync();

        // Engagement for Trending/Browse sorts and a pending interest with a full sender profile for Rahul.
        var rahulIdeas = await db.Ideas.Where(i => i.SubmitterUserId == rahul.Id).ToListAsync();
        var edu = rahulIdeas.FirstOrDefault(i => i.Title.StartsWith("EduSpark"));
        var farm = rahulIdeas.FirstOrDefault(i => i.Title.StartsWith("FarmConnect"));
        if (edu != null && !await db.Interests.AnyAsync(i => i.IdeaId == edu.Id && i.UserId == created["ananya@demo.in"].Id))
        {
            db.Interests.Add(new Interest
            {
                IdeaId = edu.Id, UserId = created["ananya@demo.in"].Id, InterestType = InterestType.Work, SelectedRoles = "Marketing,Sales",
                Message = "I grew a vernacular learning app to 50k installs through school ambassadors. Would love to own growth and school sales for EduSpark.",
                Status = InterestStatus.Pending, CreatedAt = now.AddHours(-20)
            });
        }
        if (farm != null && !await db.Interests.AnyAsync(i => i.IdeaId == farm.Id && i.UserId == created["karan@demo.in"].Id))
        {
            db.Interests.Add(new Interest
            {
                IdeaId = farm.Id, UserId = created["karan@demo.in"].Id, InterestType = InterestType.Both, SelectedRoles = "Operations",
                ProposedInvestmentAmount = 50000,
                Message = "I've run cold-chain pickups from Mysuru villages for years — can set up your logistics and put in ₹50k.",
                Status = InterestStatus.Pending, CreatedAt = now.AddHours(-30)
            });
        }
        foreach (var (email, ideaPrefix, hoursAgo) in new[]
        {
            ("ananya@demo.in", "FarmConnect", 10), ("karan@demo.in", "FarmConnect", 26), ("meera@demo.in", "EduSpark", 5),
            ("vikram@demo.in", "CraftKart", 50), ("sana@demo.in", "HealthBridge", 80)
        })
        {
            var idea = await db.Ideas.FirstOrDefaultAsync(i => i.Title.StartsWith(ideaPrefix));
            var userId = created[email].Id;
            if (idea != null && !await db.IdeaLikes.AnyAsync(l => l.IdeaId == idea.Id && l.UserId == userId))
                db.IdeaLikes.Add(new IdeaLike { IdeaId = idea.Id, UserId = userId, CreatedAt = now.AddHours(-hoursAgo) });
        }
        await db.SaveChangesAsync();
    }

    private static async Task<ApplicationUser?> EnsureMemberAsync(ApplicationDbContext db, UserManager<ApplicationUser> users, ILogger logger,
        DemoMember m, string password, Func<string, int?> categoryId)
    {
        var user = await users.FindByEmailAsync(m.Email);
        if (user != null) return user;

        user = new ApplicationUser
        {
            UserName = m.Email, Email = m.Email, FullName = m.Name, City = m.City, State = m.State, Age = m.Age,
            EmailConfirmed = true, CreatedAt = DateTime.UtcNow.AddDays(-20)
        };
        var result = await users.CreateAsync(user, password);
        if (!result.Succeeded)
        {
            logger.LogWarning("Could not seed demo account {Email}: {Errors}", m.Email, string.Join(" ", result.Errors.Select(e => e.Description)));
            return null;
        }
        await users.AddToRoleAsync(user, "Member");

        db.UserProfiles.Add(new UserProfile
        {
            UserId = user.Id,
            Bio = m.Bio,
            TimeAvailability = m.Availability,
            HoursPerWeek = m.Hours,
            InvestmentCapacity = InvestmentCapacity.UpTo10K,
            IsVerifiedFounder = m.Verified,
            LinkedInUrl = $"https://www.linkedin.com/in/{m.Name.ToLowerInvariant().Replace(".", "").Replace(' ', '-')}-demo",
            ProfileCompletionPercent = 90,
            Skills = m.Skills.Select(s => new UserSkill { SkillName = s }).ToList(),
            InterestTags = m.Interests.Select(categoryId).Where(id => id.HasValue).Select(id => new UserInterestTag { CategoryId = id!.Value }).ToList()
        });
        if (m.Private)
            db.UserSettings.Add(new UserSettings { UserId = user.Id, ProfileVisibility = ProfileVisibilityOptions.Private });
        await db.SaveChangesAsync();
        return user;
    }

    private static async Task EnsureIdeaAsync(ApplicationDbContext db, ApplicationUser owner, string title, string tagline, string description,
        string problem, string solution, string market, string business, int? categoryId, decimal fund, IdeaProgressStage stage,
        string[] roles, DateTime publishedAt)
    {
        if (categoryId == null) return;
        var prefix = title.Split(' ')[0];
        if (await db.Ideas.AnyAsync(i => i.SubmitterUserId == owner.Id && i.Title.StartsWith(prefix))) return;
        db.Ideas.Add(new Idea
        {
            SubmitterUserId = owner.Id, Title = title, Tagline = tagline, Description = description, ProblemStatement = problem,
            Solution = solution, TargetMarket = market, BusinessModel = business, CategoryId = categoryId.Value,
            MinimumFundRequired = fund, ExpectedTeamSize = 4, Status = IdeaStatus.Approved, ProgressStage = stage,
            PublishedAt = publishedAt, CreatedAt = publishedAt.AddDays(-1), UpdatedAt = publishedAt,
            RolesNeeded = roles.Select(r => new IdeaRoleNeeded { RoleName = r }).ToList()
        });
        // Recent activity feeds the "active lately" tie-breaker in team matching.
        db.UserActivities.Add(new UserActivity
        {
            UserId = owner.Id, ActionType = ActivityTypes.IdeaSubmitted, Description = $"You submitted \"{title}\" for review.",
            RelatedUrl = "/Ideas/MyIdeas", CreatedAt = publishedAt.AddDays(-1)
        });
    }
}
