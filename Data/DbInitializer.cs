using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using StartupConnect.Models;

namespace StartupConnect.Data;

public static class DbInitializer
{
    public static async Task InitializeAsync(IServiceProvider services)
    {
        var context = services.GetRequiredService<ApplicationDbContext>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();

        await context.Database.MigrateAsync();

        string[] roles = ["Admin", "Member", "Investor", "Panel"];
        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));
        }

        if (!await context.Categories.AnyAsync())
        {
            context.Categories.AddRange(
                new Category { Name = "Technology", Description = "Software, AI, IoT", IconClass = "bi-cpu" },
                new Category { Name = "Healthcare", Description = "Health & wellness startups", IconClass = "bi-heart-pulse" },
                new Category { Name = "Education", Description = "EdTech & learning platforms", IconClass = "bi-book" },
                new Category { Name = "Agriculture", Description = "AgriTech & farming solutions", IconClass = "bi-tree" },
                new Category { Name = "Social Impact", Description = "Community & social good", IconClass = "bi-people" },
                new Category { Name = "E-Commerce", Description = "Online retail & marketplaces", IconClass = "bi-cart" }
            );
            await context.SaveChangesAsync();
        }

        if (!await userManager.Users.AnyAsync())
        {
            var admin = new ApplicationUser
            {
                UserName = "admin@startupconnect.in",
                Email = "admin@startupconnect.in",
                FullName = "Platform Admin",
                City = "Mumbai",
                State = "Maharashtra",
                Age = 30,
                EmailConfirmed = true
            };
            await userManager.CreateAsync(admin, "Admin@123");
            await userManager.AddToRoleAsync(admin, "Admin");
            await userManager.AddToRoleAsync(admin, "Panel");

            var member = new ApplicationUser
            {
                UserName = "rahul@demo.in",
                Email = "rahul@demo.in",
                FullName = "Rahul Sharma",
                City = "Bangalore",
                State = "Karnataka",
                Age = 24,
                EmailConfirmed = true
            };
            await userManager.CreateAsync(member, "Demo@123");
            await userManager.AddToRoleAsync(member, "Member");

            var profile = new UserProfile
            {
                UserId = member.Id,
                Bio = "Passionate developer looking to build the next big thing.",
                TimeAvailability = TimeAvailability.FullTime,
                HoursPerWeek = 40,
                InvestmentCapacity = InvestmentCapacity.From10KTo50K,
                ProfileCompletionPercent = 85
            };
            context.UserProfiles.Add(profile);
            await context.SaveChangesAsync();

            profile.InterestTags.Add(new UserInterestTag { CategoryId = 1 });
            profile.Skills.Add(new UserSkill { SkillName = "Developer" });
            profile.Skills.Add(new UserSkill { SkillName = "Designer" });
            await context.SaveChangesAsync();

            var categories = await context.Categories.ToListAsync();

            context.Ideas.AddRange(
                new Idea
                {
                    SubmitterUserId = member.Id,
                    Title = "FarmConnect — Direct Farm to Home",
                    Tagline = "Fresh produce delivered from local farmers to your doorstep",
                    Description = "A mobile platform connecting farmers directly with urban consumers, eliminating middlemen.",
                    ProblemStatement = "Farmers get low prices; consumers pay high prices due to multiple intermediaries.",
                    Solution = "Direct marketplace with logistics support and fair pricing for both sides.",
                    TargetMarket = "Urban households in Tier 1 & 2 cities",
                    BusinessModel = "Commission on transactions + subscription for premium delivery",
                    CategoryId = categories.First(c => c.Name == "Agriculture").Id,
                    MinimumFundRequired = 500000,
                    ExpectedTeamSize = 5,
                    Status = IdeaStatus.Approved,
                    PublishedAt = DateTime.UtcNow.AddDays(-5),
                    RolesNeeded = new List<IdeaRoleNeeded>
                    {
                        new() { RoleName = "Developer" },
                        new() { RoleName = "Marketing" },
                        new() { RoleName = "Operations" }
                    }
                },
                new Idea
                {
                    SubmitterUserId = member.Id,
                    Title = "EduSpark — Vernacular Learning App",
                    Tagline = "Quality education in Hindi, Tamil, and regional languages",
                    Description = "An app offering curated courses in regional languages for students in rural India.",
                    ProblemStatement = "Quality educational content is mostly available only in English.",
                    Solution = "Localized video courses with offline access and affordable pricing.",
                    TargetMarket = "Students aged 15-25 in Tier 2/3 cities",
                    BusinessModel = "Freemium + paid courses + B2B school licensing",
                    CategoryId = categories.First(c => c.Name == "Education").Id,
                    MinimumFundRequired = 300000,
                    ExpectedTeamSize = 4,
                    Status = IdeaStatus.Approved,
                    PublishedAt = DateTime.UtcNow.AddDays(-3),
                    RolesNeeded = new List<IdeaRoleNeeded>
                    {
                        new() { RoleName = "Developer" },
                        new() { RoleName = "Content Creator" },
                        new() { RoleName = "Sales" }
                    }
                },
                new Idea
                {
                    SubmitterUserId = member.Id,
                    Title = "HealthBridge — Rural Telemedicine",
                    Tagline = "Affordable doctor consultations via video for rural India",
                    Description = "Connect patients in villages with qualified doctors through low-bandwidth video calls.",
                    ProblemStatement = "Rural areas lack access to qualified healthcare professionals.",
                    Solution = "Telemedicine platform optimized for 2G/3G with local health worker support.",
                    TargetMarket = "Rural population across India",
                    BusinessModel = "Per consultation fee + government/NGO partnerships",
                    CategoryId = categories.First(c => c.Name == "Healthcare").Id,
                    MinimumFundRequired = 800000,
                    ExpectedTeamSize = 6,
                    Status = IdeaStatus.Approved,
                    PublishedAt = DateTime.UtcNow.AddDays(-1),
                    RolesNeeded = new List<IdeaRoleNeeded>
                    {
                        new() { RoleName = "Developer" },
                        new() { RoleName = "Medical Advisor" },
                        new() { RoleName = "Operations" }
                    }
                }
            );
            await context.SaveChangesAsync();
        }
    }
}
