using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using StartupConnect.Models;
using StartupConnect.Services;

namespace StartupConnect.Data;

/// <summary>
/// Development demo data for challenges, team workspaces, milestones and moderation. Every block is
/// idempotent (checks for its own rows) so it can run on every start against an existing dev DB.
/// Accounts: priya@demo.in (designer, pending founder verification) and arjun@demo.in (developer),
/// both with the demo password. Rahul (rahul@demo.in) founds the "FarmConnect" demo team.
/// </summary>
public static class CollaborationSeed
{
    public const string PriyaEmail = "priya@demo.in";
    public const string ArjunEmail = "arjun@demo.in";

    public static async Task SeedAsync(ApplicationDbContext db, UserManager<ApplicationUser> users, string demoPassword, ILogger logger)
    {
        var rahul = await users.FindByEmailAsync("rahul@demo.in");
        if (rahul == null) return; // base demo data missing (seeding disabled or failed)

        var priya = await EnsureUserAsync(db, users, logger, PriyaEmail, "Priya Nair", "Kochi", "Kerala", 26, demoPassword,
            "Product designer who loves turning messy real-world problems into simple apps. Ex-Swiggy, now building for Bharat.",
            ["Designer", "Marketing"], "Social Impact", TimeAvailability.PartTime, 20);
        var arjun = await EnsureUserAsync(db, users, logger, ArjunEmail, "Arjun Mehta", "Pune", "Maharashtra", 28, demoPassword,
            "Full-stack developer (React, .NET, Flutter). Looking for an early-stage team with a real problem to solve.",
            ["Developer", "Operations"], "Agriculture", TimeAvailability.FullTime, 35);
        if (priya == null || arjun == null) return;

        var categories = await db.Categories.ToListAsync();
        int CategoryId(string name) => categories.FirstOrDefault(c => c.Name == name)?.Id ?? categories.First().Id;

        // ---- Priya's approved idea ----
        var craftKart = await db.Ideas.FirstOrDefaultAsync(i => i.SubmitterUserId == priya.Id && i.Title.StartsWith("CraftKart"));
        if (craftKart == null)
        {
            craftKart = new Idea
            {
                SubmitterUserId = priya.Id,
                Title = "CraftKart — Artisan Marketplace",
                Tagline = "Helping rural artisans sell handmade products directly to city buyers",
                Description = "A vernacular-first marketplace where artisans list products by voice and photo, with doorstep pickup and fair, transparent pricing.",
                ProblemStatement = "Artisans earn a fraction of the retail price because traders and resellers control access to urban buyers.",
                Solution = "Voice-based listing in regional languages, logistics partnerships for pickup, and a storytelling storefront for each artisan.",
                TargetMarket = "Artisans in Kerala, Rajasthan and Odisha; conscious urban shoppers aged 22–40",
                BusinessModel = "12% commission per order + paid promotion for artisan cooperatives",
                CategoryId = CategoryId("E-Commerce"),
                MinimumFundRequired = 400000,
                ExpectedTeamSize = 4,
                Status = IdeaStatus.Approved,
                PublishedAt = DateTime.UtcNow.AddDays(-12),
                CreatedAt = DateTime.UtcNow.AddDays(-14),
                RolesNeeded = new List<IdeaRoleNeeded> { new() { RoleName = "Developer" }, new() { RoleName = "Operations" } }
            };
            db.Ideas.Add(craftKart);
            await db.SaveChangesAsync();
        }

        await SeedTeamAsync(db, rahul, priya, arjun);
        await SeedChallengesAsync(db, rahul, craftKart, CategoryId);
        await SeedModerationAsync(db, rahul, priya, arjun, craftKart);
    }

    private static async Task<ApplicationUser?> EnsureUserAsync(ApplicationDbContext db, UserManager<ApplicationUser> users, ILogger logger,
        string email, string name, string city, string state, int age, string password, string bio, string[] skills,
        string interestCategory, TimeAvailability availability, int hours)
    {
        var user = await users.FindByEmailAsync(email);
        if (user != null) return user;

        user = new ApplicationUser
        {
            UserName = email, Email = email, FullName = name, City = city, State = state, Age = age,
            EmailConfirmed = true, CreatedAt = DateTime.UtcNow.AddDays(-30)
        };
        var result = await users.CreateAsync(user, password);
        if (!result.Succeeded)
        {
            logger.LogWarning("Could not seed demo account {Email}: {Errors}", email, string.Join(" ", result.Errors.Select(e => e.Description)));
            return null;
        }
        await users.AddToRoleAsync(user, "Member");

        var categoryId = await db.Categories.Where(c => c.Name == interestCategory).Select(c => c.Id).FirstOrDefaultAsync();
        var profile = new UserProfile
        {
            UserId = user.Id,
            Bio = bio,
            TimeAvailability = availability,
            HoursPerWeek = hours,
            InvestmentCapacity = InvestmentCapacity.UpTo10K,
            LinkedInUrl = $"https://www.linkedin.com/in/{name.ToLowerInvariant().Replace(' ', '-')}-demo",
            ProfileCompletionPercent = 85,
            Skills = skills.Select(s => new UserSkill { SkillName = s }).ToList()
        };
        if (categoryId != 0) profile.InterestTags.Add(new UserInterestTag { CategoryId = categoryId });
        db.UserProfiles.Add(profile);
        await db.SaveChangesAsync();
        return user;
    }

    private static async Task SeedTeamAsync(ApplicationDbContext db, ApplicationUser rahul, ApplicationUser priya, ApplicationUser arjun)
    {
        var farm = await db.Ideas.FirstOrDefaultAsync(i => i.SubmitterUserId == rahul.Id && i.Title.StartsWith("FarmConnect"));
        if (farm == null || await db.Teams.AnyAsync(t => t.IdeaId == farm.Id)) return;

        var now = DateTime.UtcNow;
        var team = new Team { IdeaId = farm.Id, Name = "FarmConnect Team", Status = TeamStatus.Active, CreatedAt = now.AddDays(-9) };
        db.Teams.Add(team);
        await db.SaveChangesAsync();

        db.TeamMembers.AddRange(
            new TeamMember { TeamId = team.Id, UserId = rahul.Id, Role = TeamRoles.Founder, JoinedAt = now.AddDays(-9) },
            new TeamMember { TeamId = team.Id, UserId = priya.Id, Role = "Designer", JoinedAt = now.AddDays(-9) },
            new TeamMember { TeamId = team.Id, UserId = arjun.Id, Role = "Developer", JoinedAt = now.AddDays(-7) });

        foreach (var (member, role, type, days) in new[] { (priya, "Designer", InterestType.Work, 10), (arjun, "Developer", InterestType.Both, 8) })
        {
            if (!await db.Interests.AnyAsync(i => i.IdeaId == farm.Id && i.UserId == member.Id))
            {
                db.Interests.Add(new Interest
                {
                    IdeaId = farm.Id, UserId = member.Id, InterestType = type, SelectedRoles = role, Status = InterestStatus.Accepted,
                    ProposedInvestmentAmount = type == InterestType.Both ? 25000 : null,
                    Message = type == InterestType.Both ? "I can build the app and put in a small cheque." : "Would love to design the farmer onboarding flow.",
                    CreatedAt = now.AddDays(-days)
                });
            }
        }

        db.IdeaMilestones.AddRange(
            new IdeaMilestone { IdeaId = farm.Id, Title = "Interview 25 farmers in Mysuru district", Description = "Validate pricing pain points and how farmers sell today.", DueDate = now.AddDays(-5).Date, IsCompleted = true, CompletedAt = now.AddDays(-5), AssigneeUserId = rahul.Id, CreatedByUserId = rahul.Id, CreatedAt = now.AddDays(-9) },
            new IdeaMilestone { IdeaId = farm.Id, Title = "Finish MVP design", Description = "Farmer listing flow in Kannada + buyer checkout, clickable in Figma.", DueDate = now.AddDays(-2).Date, IsCompleted = true, CompletedAt = now.AddDays(-1), AssigneeUserId = priya.Id, CreatedByUserId = rahul.Id, CreatedAt = now.AddDays(-9) },
            new IdeaMilestone { IdeaId = farm.Id, Title = "Build ordering MVP", Description = "Flutter app with catalogue, cart and UPI payments.", DueDate = now.AddDays(14).Date, AssigneeUserId = arjun.Id, CreatedByUserId = arjun.Id, CreatedAt = now.AddDays(-6) },
            new IdeaMilestone { IdeaId = farm.Id, Title = "Sign 2 logistics partners", Description = "Cold-chain pickup from 3 villages twice a week.", DueDate = now.AddDays(21).Date, AssigneeUserId = rahul.Id, CreatedByUserId = rahul.Id, CreatedAt = now.AddDays(-4) },
            new IdeaMilestone { IdeaId = farm.Id, Title = "Pilot with 50 households in Bengaluru", Description = "Two-week pilot, measure repeat orders and delivery time.", DueDate = now.AddDays(40).Date, CreatedByUserId = rahul.Id, CreatedAt = now.AddDays(-4) });

        var chat = new (ApplicationUser Who, double HoursAgo, string Text)[]
        {
            (rahul, 30, "Welcome aboard Priya and Arjun! 🎉 This is our space for FarmConnect — let's keep decisions here."),
            (priya, 29.5, "Thanks Rahul! I'll start with the farmer onboarding flow. Can you share the interview notes?"),
            (rahul, 29, "Uploaded them to our drive. Biggest pain: middlemen take 30–40%, and farmers don't trust app payments yet."),
            (arjun, 26, "Then UPI with instant SMS confirmation is a must. I'll prototype that first."),
            (priya, 20, "MVP design is done ✅ — Kannada-first, big buttons, voice prompts for listing produce."),
            (rahul, 3, "Great work. I'm meeting a cold-chain partner in Mandya on Friday."),
            (arjun, 1, "Catalogue + cart are working on staging. Payments next week.")
        };
        foreach (var (who, hoursAgo, text) in chat)
            db.TeamMessages.Add(new TeamMessage { TeamId = team.Id, SenderUserId = who.Id, Content = text, SentAt = now.AddHours(-hoursAgo) });

        db.UserActivities.AddRange(
            new UserActivity { UserId = rahul.Id, ActionType = ActivityTypes.TeamCreated, Description = "You created the team workspace for \"FarmConnect — Direct Farm to Home\".", RelatedUrl = $"/Workspace/Team/{team.Id}", CreatedAt = now.AddDays(-9) },
            new UserActivity { UserId = rahul.Id, ActionType = ActivityTypes.MilestoneCompleted, Description = "You completed the milestone \"Interview 25 farmers in Mysuru district\" for FarmConnect.", RelatedUrl = $"/Workspace/Team/{team.Id}", CreatedAt = now.AddDays(-5) },
            new UserActivity { UserId = priya.Id, ActionType = ActivityTypes.JoinedTeam, Description = "You joined the team for \"FarmConnect — Direct Farm to Home\".", RelatedUrl = $"/Workspace/Team/{team.Id}", CreatedAt = now.AddDays(-9) },
            new UserActivity { UserId = priya.Id, ActionType = ActivityTypes.MilestoneCompleted, Description = "You completed the milestone \"Finish MVP design\" for FarmConnect.", RelatedUrl = $"/Workspace/Team/{team.Id}", CreatedAt = now.AddDays(-1) },
            new UserActivity { UserId = arjun.Id, ActionType = ActivityTypes.JoinedTeam, Description = "You joined the team for \"FarmConnect — Direct Farm to Home\".", RelatedUrl = $"/Workspace/Team/{team.Id}", CreatedAt = now.AddDays(-7) });

        await db.SaveChangesAsync();
    }

    private static async Task SeedChallengesAsync(ApplicationDbContext db, ApplicationUser rahul, Idea craftKart, Func<string, int> categoryId)
    {
        if (await db.StartupChallenges.AnyAsync()) return;

        var now = DateTime.UtcNow;
        // Deadlines at 23:59 IST.
        DateTime IstEndOfDay(int daysFromNow) => Infrastructure.AppTime.IstToUtc(Infrastructure.AppTime.ToIst(now).Date.AddDays(daysFromNow).AddHours(23).AddMinutes(59));

        var agri = new StartupChallenge
        {
            Title = "AgriTech Innovation Challenge 2026",
            Description = "India's farmers feed 1.4 billion people but capture a small share of what consumers pay. We're looking for ideas that raise farmer incomes — better market access, post-harvest storage, credit, or climate resilience.\n\nShortlisted teams pitch live to a jury of agri-investors and FPO leaders.",
            Prize = "₹2,00,000 + 3-month incubation",
            Deadline = IstEndOfDay(21),
            CategoryId = categoryId("Agriculture"),
            Eligibility = "Teams of 1–5 with an approved idea on StartupConnect. At least one founder must be under 35.",
            Rules = "Judged on impact on farmer income (40%), feasibility (30%), team (20%) and originality (10%). One entry per idea. Winners announced within two weeks of the deadline.",
            CreatedAt = now.AddDays(-7)
        };
        var edtech = new StartupChallenge
        {
            Title = "Bharat EdTech Sprint",
            Description = "Build for the next 300 million learners: vernacular content, low-bandwidth delivery, teacher tools or skilling for Tier 2/3 towns. Tell us how your idea reaches learners that today's apps miss.",
            Prize = "₹1,00,000 + cloud credits",
            Deadline = IstEndOfDay(9),
            CategoryId = categoryId("Education"),
            Eligibility = "Open to all StartupConnect founders with an approved EdTech or skilling idea.",
            Rules = "Pitch notes must explain your first 1,000 learners and how you'll reach them. The jury may contact shortlisted founders for a 15-minute call.",
            CreatedAt = now.AddDays(-3)
        };
        var impact = new StartupChallenge
        {
            Title = "Social Impact Pitch-Off 2026",
            Description = "Ideas that improve healthcare, livelihoods or dignity for underserved communities. Winners receive a grant and mentorship from our partner foundations.",
            Prize = "₹50,000 grant + mentorship",
            Deadline = IstEndOfDay(-20),
            CategoryId = categoryId("Social Impact"),
            Eligibility = "Any approved idea with a clear social outcome.",
            Rules = "Judged on measurable impact, sustainability of the model and clarity of the pitch.",
            IsActive = false,
            ResultsAnnouncedAt = now.AddDays(-10),
            CreatedAt = now.AddDays(-50)
        };
        db.StartupChallenges.AddRange(agri, edtech, impact);
        await db.SaveChangesAsync();

        var farm = await db.Ideas.FirstOrDefaultAsync(i => i.SubmitterUserId == rahul.Id && i.Title.StartsWith("FarmConnect"));
        var health = await db.Ideas.FirstOrDefaultAsync(i => i.SubmitterUserId == rahul.Id && i.Title.StartsWith("HealthBridge"));
        if (farm != null)
        {
            db.ChallengeSubmissions.Add(new ChallengeSubmission
            {
                ChallengeId = agri.Id, IdeaId = farm.Id, SubmittedAt = now.AddDays(-2),
                PitchNotes = "We interviewed 25 farmers in Mysuru: middlemen take 30–40% of the price. FarmConnect lets them sell directly with same-week UPI payouts. Team of 3, MVP design done, ordering app in progress; the prize funds our 50-household pilot."
            });
            db.UserActivities.Add(new UserActivity { UserId = rahul.Id, ActionType = ActivityTypes.ChallengeSubmitted, Description = $"You submitted \"{farm.Title}\" to {agri.Title}.", RelatedUrl = $"/Challenges/Details/{agri.Id}", CreatedAt = now.AddDays(-2) });
        }
        if (health != null)
        {
            db.ChallengeSubmissions.Add(new ChallengeSubmission
            {
                ChallengeId = impact.Id, IdeaId = health.Id, SubmittedAt = now.AddDays(-28), IsShortlisted = true, IsWinner = true, AwardTitle = "Winner",
                JudgeFeedback = "Strong grasp of the rural last mile. Partner with ASHA workers early.",
                PitchNotes = "Low-bandwidth video consultations run through village health workers. Pilot plan with 2 PHCs in Karnataka."
            });
            db.UserActivities.Add(new UserActivity { UserId = rahul.Id, ActionType = ActivityTypes.ChallengeResult, Description = $"\"{health.Title}\" {ChallengeService.WonPhrase("Winner", impact.Title)}.", RelatedUrl = $"/Challenges/Details/{impact.Id}", CreatedAt = now.AddDays(-10) });
        }
        db.ChallengeSubmissions.Add(new ChallengeSubmission
        {
            ChallengeId = impact.Id, IdeaId = craftKart.Id, SubmittedAt = now.AddDays(-25), IsShortlisted = true,
            JudgeFeedback = "Great artisan stories — show us repeat-purchase data next time.",
            PitchNotes = "Voice-first listings in Malayalam for 60 artisans in our first cooperative."
        });
        await db.SaveChangesAsync();
    }

    private static async Task SeedModerationAsync(ApplicationDbContext db, ApplicationUser rahul, ApplicationUser priya, ApplicationUser arjun, Idea craftKart)
    {
        var now = DateTime.UtcNow;
        if (!await db.FounderVerificationRequests.AnyAsync())
        {
            var admin = await db.Users.Where(u => u.Email == "admin@startupconnect.in").Select(u => u.Id).FirstOrDefaultAsync();
            db.FounderVerificationRequests.AddRange(
                new FounderVerificationRequest
                {
                    UserId = rahul.Id, Note = "Founder of FarmConnect and HealthBridge. Ex-Infosys engineer, working full-time on agri-marketplaces in Karnataka.",
                    LinkedInUrl = "https://www.linkedin.com/in/rahul-sharma-demo", Status = VerificationRequestStatus.Approved,
                    CreatedAt = now.AddDays(-15), ReviewedAt = now.AddDays(-14), ReviewedByUserId = admin
                },
                new FounderVerificationRequest
                {
                    UserId = priya.Id, Note = "I'm building CraftKart with a women's artisan cooperative in Kochi — 60 artisans onboarded for our pilot.",
                    LinkedInUrl = "https://www.linkedin.com/in/priya-nair-demo", Status = VerificationRequestStatus.Pending, CreatedAt = now.AddHours(-20)
                });
            var rahulProfile = await db.UserProfiles.FirstOrDefaultAsync(p => p.UserId == rahul.Id);
            if (rahulProfile != null) rahulProfile.IsVerifiedFounder = true;
            var priyaProfile = await db.UserProfiles.FirstOrDefaultAsync(p => p.UserId == priya.Id);
            if (priyaProfile != null) priyaProfile.VerificationRequested = true;
            db.UserActivities.Add(new UserActivity { UserId = rahul.Id, ActionType = ActivityTypes.VerifiedFounder, Description = "You became a Verified Founder.", RelatedUrl = $"/Profile/Detail/{rahul.Id}", CreatedAt = now.AddDays(-14) });
            await db.SaveChangesAsync();
        }

        if (!await db.IdeaReports.AnyAsync())
        {
            db.IdeaReports.Add(new IdeaReport
            {
                IdeaId = craftKart.Id, UserId = arjun.Id, CreatedAt = now.AddHours(-6),
                Reason = "Misleading or inaccurate — the commission and pricing claims look copied from another marketplace listing."
            });
            await db.SaveChangesAsync();
        }
    }
}
