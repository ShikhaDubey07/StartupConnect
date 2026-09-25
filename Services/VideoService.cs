using StartupConnect.ViewModels;

namespace StartupConnect.Services;

public class VideoService : IVideoService
{
    private static readonly List<VideoItemViewModel> AllVideos = new()
    {
        // 1. Featured Spotlight: How to Pitch
        new VideoItemViewModel
        {
            Id = "how-to-pitch-yc",
            Title = "How to Pitch Your Startup to Investors & Angels",
            Description = "Master the art of pitching early-stage ideas. Learn the exact 3-minute pitch framework used by top YC founders to secure angel and seed checks.",
            Category = "Pitching & Funding",
            Duration = "18:24",
            DurationMinutes = 18,
            Speaker = "Kevin Hale",
            SpeakerRole = "Partner @ Y Combinator",
            ThumbnailUrl = "https://images.unsplash.com/photo-1557804506-669a67965ba0?auto=format&fit=crop&w=800&q=80",
            YouTubeId = "C27RVio2rOs",
            Views = "482K",
            Level = "Masterclass",
            IsFeatured = true,
            PublishedDate = "Updated 2026",
            Tags = new List<string> { "Pitch Deck", "Seed Round", "Angel Investing", "Storytelling" },
            KeyTakeaways = new List<string>
            {
                "Explain what you do in one clear, simple sentence without buzzwords.",
                "Highlight traction, market size, and your unique founder advantage.",
                "Avoid complex feature dumps; focus on customer problem and proven willingness to pay."
            }
        },

        // 2. Co-Founders & Team
        new VideoItemViewModel
        {
            Id = "how-to-choose-cofounder",
            Title = "How to Choose and Work with a Co-Founder",
            Description = "Finding the right co-founder is the most critical decision in your startup's journey. Learn how to evaluate complement skills, values alignment, and trust.",
            Category = "Co-Founders & Team",
            Duration = "21:15",
            DurationMinutes = 21,
            Speaker = "Dalton Caldwell & Michael Seibel",
            SpeakerRole = "Managing Directors @ YC",
            ThumbnailUrl = "https://images.unsplash.com/photo-1522071820081-009f0129c71c?auto=format&fit=crop&w=800&q=80",
            YouTubeId = "55_hGvO4i6Y",
            Views = "310K",
            Level = "Beginner",
            IsFeatured = false,
            PublishedDate = "2025",
            Tags = new List<string> { "Co-Founder", "Team Building", "Culture", "Startup Equity" },
            KeyTakeaways = new List<string>
            {
                "Look for complementary skills (e.g. Technical + Go-To-Market builder).",
                "Work on a trial project before signing equity agreements.",
                "Ensure deep alignment on long-term commitment, ambition, and work ethics."
            }
        },

        // 3. Co-Founders & Equity Split
        new VideoItemViewModel
        {
            Id = "equity-split-early-hires",
            Title = "Splitting Startup Equity Among Co-Founders Without Conflict",
            Description = "How to handle equity splits, 4-year vesting schedules with a 1-year cliff, and avoiding cap table ruin before your first financing round.",
            Category = "Co-Founders & Team",
            Duration = "15:40",
            DurationMinutes = 15,
            Speaker = "Michael Seibel",
            SpeakerRole = "Co-Founder Twitch & YC Partner",
            ThumbnailUrl = "https://images.unsplash.com/photo-1551836022-d5d88e9218df?auto=format&fit=crop&w=800&q=80",
            YouTubeId = "7d13K-jQ2V0",
            Views = "225K",
            Level = "Intermediate",
            IsFeatured = false,
            PublishedDate = "2025",
            Tags = new List<string> { "Cap Table", "Equity", "Vesting", "Founders Agreement" },
            KeyTakeaways = new List<string>
            {
                "Always mandate standard 4-year vesting with a 1-year cliff for all co-founders.",
                "Avoid heavily asymmetric splits unless one founder has proven intellectual property.",
                "Draft formal legal founder agreements early to avoid future dilution disputes."
            }
        },

        // 4. Product Strategy & MVP
        new VideoItemViewModel
        {
            Id = "how-to-build-mvp-fast",
            Title = "How to Build an MVP in 2 Weeks and Validate Real Demand",
            Description = "Stop over-engineering! Learn how to launch a rapid Minimum Viable Product, get first paying customers, and iterate rapidly based on real user feedback.",
            Category = "Product & MVP",
            Duration = "16:50",
            DurationMinutes = 17,
            Speaker = "Michael Seibel",
            SpeakerRole = "YC Startup School",
            ThumbnailUrl = "https://images.unsplash.com/photo-1531403009284-440f080d1e12?auto=format&fit=crop&w=800&q=80",
            YouTubeId = "1hHMwLxN6EM",
            Views = "540K",
            Level = "Beginner",
            IsFeatured = false,
            PublishedDate = "2025",
            Tags = new List<string> { "MVP", "Product Strategy", "Fast Execution", "Lean Startup" },
            KeyTakeaways = new List<string>
            {
                "An MVP is a process, not just a one-off product release.",
                "Build only the single core feature that solves the user's primary hair-on-fire problem.",
                "Talk directly to your first 10-50 users weekly to refine the user experience."
            }
        },

        // 5. Product & Customer Discovery
        new VideoItemViewModel
        {
            Id = "how-to-talk-to-users",
            Title = "Talking to Users & Customer Discovery: The Mom Test Approach",
            Description = "How to ask the right questions without biasing customer responses. Learn whether people genuinely want your product or are just being polite.",
            Category = "Product & MVP",
            Duration = "24:30",
            DurationMinutes = 25,
            Speaker = "Rob Fitzpatrick",
            SpeakerRole = "Author of The Mom Test",
            ThumbnailUrl = "https://images.unsplash.com/photo-1556761175-5973dc0f32e7?auto=format&fit=crop&w=800&q=80",
            YouTubeId = "MT4TgJBmqpk",
            Views = "390K",
            Level = "Intermediate",
            IsFeatured = false,
            PublishedDate = "2025",
            Tags = new List<string> { "User Research", "Customer Discovery", "The Mom Test", "Validation" },
            KeyTakeaways = new List<string>
            {
                "Ask about their past behavior and actual spending, not hypothetical future promises.",
                "Dig into specific real-world instances of how they currently solve the problem.",
                "Listen 80% of the time, talk 20% of the time during interviews."
            }
        },

        // 6. Growth & Customer Acquisition
        new VideoItemViewModel
        {
            Id = "first-1000-users-zero-budget",
            Title = "How to Acquire Your First 1,000 Users with Zero Ad Spend",
            Description = "Actionable zero-budget growth strategies: direct outreach, community distribution, viral referral hooks, and content flywheels for early traction.",
            Category = "Growth & Marketing",
            Duration = "22:45",
            DurationMinutes = 23,
            Speaker = "Gustaf Alströmer",
            SpeakerRole = "Partner @ YC & Former Head of Growth @ Airbnb",
            ThumbnailUrl = "https://images.unsplash.com/photo-1460925895917-afdab827c52f?auto=format&fit=crop&w=800&q=80",
            YouTubeId = "5rI1bM0b8o8",
            Views = "415K",
            Level = "Intermediate",
            IsFeatured = false,
            PublishedDate = "2025",
            Tags = new List<string> { "Organic Growth", "First 1000 Users", "Viral Loops", "Cold Outreach" },
            KeyTakeaways = new List<string>
            {
                "Do things that don't scale: recruit your earliest users individually and manually.",
                "Hang out where your target users already congregate online and offline.",
                "Create a high-converting onboarding experience with an immediate 'aha!' moment."
            }
        },

        // 7. B2B Sales & Go-To-Market
        new VideoItemViewModel
        {
            Id = "b2b-sales-for-founders",
            Title = "B2B Sales for Founders: From Cold Outreach to Closed Deals",
            Description = "A complete founder-led sales masterclass. How technical and non-sales founders can close their first 10 enterprise and mid-market contracts.",
            Category = "Growth & Marketing",
            Duration = "28:10",
            DurationMinutes = 28,
            Speaker = "Tyler Bosmeny",
            SpeakerRole = "CEO @ Clever & YC Startup Sales Expert",
            ThumbnailUrl = "https://images.unsplash.com/photo-1552664730-d307ca884978?auto=format&fit=crop&w=800&q=80",
            YouTubeId = "q1g6_y0jN_M",
            Views = "198K",
            Level = "Masterclass",
            IsFeatured = false,
            PublishedDate = "2025",
            Tags = new List<string> { "B2B Sales", "Cold Email", "Enterprise Contracts", "SaaS Pricing" },
            KeyTakeaways = new List<string>
            {
                "Founder-led sales creates the fastest feedback loop between sales and product engineering.",
                "Qualify prospects early on budget, authority, need, and urgency.",
                "Always ask for the close and agree on next calendar dates before ending any demo call."
            }
        },

        // 8. Indian Startup Unicorn Stories & Case Studies
        new VideoItemViewModel
        {
            Id = "zerodha-bootstrapped-revolution",
            Title = "Steve Jobs on Building Startups & Making Something Great",
            Description = "Steve Jobs breaks down the foundational mindset needed to build groundbreaking companies, create world-class products, and stay resilient through challenges.",
            Category = "Founder Case Studies",
            Duration = "34:12",
            DurationMinutes = 34,
            Speaker = "Steve Jobs",
            SpeakerRole = "Co-Founder @ Apple & NeXT",
            ThumbnailUrl = "https://images.unsplash.com/photo-1611974789855-9c2a0a7236a3?auto=format&fit=crop&w=800&q=80",
            YouTubeId = "kYfNvmF0Bqw",
            Views = "1.2M",
            Level = "Masterclass",
            IsFeatured = false,
            PublishedDate = "2025",
            Tags = new List<string> { "Steve Jobs", "Innovation", "Product Design", "Founder Mindset" },
            KeyTakeaways = new List<string>
            {
                "Everything around you that you call life was made up by people no smarter than you.",
                "Focus on delighting users and building unmatched product quality.",
                "Build for the long-term without compromising your core values."
            }
        },

        // 9. Startup Scaling Case Study
        new VideoItemViewModel
        {
            Id = "razorpay-scaling-india",
            Title = "How Great Tech Companies Scale from Zero to Millions of Users",
            Description = "Learn the core operating principles behind hyper-growth tech startups: scaling engineering architectures, developer APIs, and cross-functional teams.",
            Category = "Founder Case Studies",
            Duration = "29:45",
            DurationMinutes = 30,
            Speaker = "Sam Altman & Dustin Moskovitz",
            SpeakerRole = "YC President & Co-Founder Asana/Facebook",
            ThumbnailUrl = "https://images.unsplash.com/photo-1559526324-4b87b5e36e44?auto=format&fit=crop&w=800&q=80",
            YouTubeId = "W608U63725Y",
            Views = "480K",
            Level = "Intermediate",
            IsFeatured = false,
            PublishedDate = "2025",
            Tags = new List<string> { "Scale-Up", "Engineering", "Leadership", "Company Building" },
            KeyTakeaways = new List<string>
            {
                "Focus on delighting developers with clean APIs and instant integration sandboxes.",
                "Turn regulatory compliance and platform integrations into competitive moats.",
                "Hire leadership that scales with your 10x growth phases."
            }
        },

        // 10. Legal, Valuation & Term Sheets
        new VideoItemViewModel
        {
            Id = "seed-valuation-and-term-sheets",
            Title = "Understanding Seed Valuations, Term Sheets & SAFE Notes",
            Description = "Demystifying startup finances: Pre-money vs Post-money valuation, SAFE vs Convertible Notes, dilution calculators, and liquidation preferences.",
            Category = "Legal & Valuation",
            Duration = "20:15",
            DurationMinutes = 20,
            Speaker = "Kirsty Nathoo",
            SpeakerRole = "CFO & Partner @ Y Combinator",
            ThumbnailUrl = "https://images.unsplash.com/photo-1450133064473-71024230f91b?auto=format&fit=crop&w=800&q=80",
            YouTubeId = "wXb4e4pEa0k",
            Views = "285K",
            Level = "Intermediate",
            IsFeatured = false,
            PublishedDate = "2025",
            Tags = new List<string> { "Valuation", "Term Sheets", "SAFE Notes", "Dilution" },
            KeyTakeaways = new List<string>
            {
                "Keep your seed funding paperwork clean using standardized SAFE agreements.",
                "Understand that a higher valuation isn't always better if it sets unrealistic Series A milestones.",
                "Watch out for aggressive liquidation preferences and board control clauses."
            }
        },

        // 11. Pitch Deck Structure
        new VideoItemViewModel
        {
            Id = "10-slide-pitch-deck",
            Title = "The Essential 10-Slide Pitch Deck Framework for Seed Funding",
            Description = "The proven anatomy of a winning seed-stage investor presentation. Step-by-step guidance on every slide from problem, solution, market size, to unit economics.",
            Category = "Pitching & Funding",
            Duration = "14:30",
            DurationMinutes = 15,
            Speaker = "Guy Kawasaki",
            SpeakerRole = "Venture Capitalist & Chief Evangelist",
            ThumbnailUrl = "https://images.unsplash.com/photo-1475721027785-f74eccf877e2?auto=format&fit=crop&w=800&q=80",
            YouTubeId = "j30kSg8Z07Y",
            Views = "750K",
            Level = "Beginner",
            IsFeatured = false,
            PublishedDate = "2025",
            Tags = new List<string> { "Pitch Deck", "Investor Deck", "Slide Structure", "Presentation" },
            KeyTakeaways = new List<string>
            {
                "Keep text minimal; use strong visuals and clean charts.",
                "State your target addressable market (TAM) with realistic bottoms-up logic.",
                "State clearly how much capital you are raising and what milestones it unlocks over 18 months."
            }
        },

        // 12. Indian Startup Incorporation & Legal Clinic
        new VideoItemViewModel
        {
            Id = "india-startup-legal-guide",
            Title = "Startup Incorporation, DPIIT Registration & Tax Benefits in India",
            Description = "Comprehensive legal guide for Indian youth founders: Private Limited company registration, DPIIT startup recognition, Section 80-IAC tax exemptions, and patent rebates.",
            Category = "Legal & Valuation",
            Duration = "26:50",
            DurationMinutes = 27,
            Speaker = "Startup India Legal Advisory Panel",
            SpeakerRole = "Legal Advisory & Startup India Mentors",
            ThumbnailUrl = "https://images.unsplash.com/photo-1589829545856-d10d557cf95f?auto=format&fit=crop&w=800&q=80",
            YouTubeId = "L3xaeZID97I",
            Views = "340K",
            Level = "Beginner",
            IsFeatured = false,
            PublishedDate = "2026",
            Tags = new List<string> { "Startup India", "DPIIT", "Pvt Ltd", "Tax Exemption", "Legal" },
            KeyTakeaways = new List<string>
            {
                "Incorporate as a Private Limited Company if you plan on raising venture or angel capital.",
                "Register on the Startup India portal for DPIIT certification and Angel Tax exemptions.",
                "Draft clear Employment and Non-Disclosure Agreements (NDA) for all developers and designers."
            }
        }
    };

    public Task<VideosIndexViewModel> GetVideosAsync(string? category, string? search, string? sort)
    {
        var query = AllVideos.AsQueryable();

        // 1. Category Filter
        if (!string.IsNullOrWhiteSpace(category) && !category.Equals("All", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(v => v.Category.Equals(category, StringComparison.OrdinalIgnoreCase));
        }

        // 2. Search Filter
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLowerInvariant();
            query = query.Where(v =>
                v.Title.ToLower().Contains(s) ||
                v.Description.ToLower().Contains(s) ||
                v.Speaker.ToLower().Contains(s) ||
                v.SpeakerRole.ToLower().Contains(s) ||
                v.Tags.Any(t => t.ToLower().Contains(s)));
        }

        // 3. Sorting
        var list = query.ToList();
        list = sort switch
        {
            "popular" => list.OrderByDescending(v => v.Views).ToList(),
            "shortest" => list.OrderBy(v => v.DurationMinutes).ToList(),
            "longest" => list.OrderByDescending(v => v.DurationMinutes).ToList(),
            "level" => list.OrderBy(v => v.Level).ToList(),
            _ => list.OrderByDescending(v => v.IsFeatured).ThenBy(v => v.Title).ToList()
        };

        // Category Counts
        var categoryCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["All"] = AllVideos.Count
        };

        foreach (var group in AllVideos.GroupBy(v => v.Category))
        {
            categoryCounts[group.Key] = group.Count();
        }

        var featured = AllVideos.FirstOrDefault(v => v.IsFeatured) ?? AllVideos.First();

        var viewModel = new VideosIndexViewModel
        {
            Videos = list,
            FeaturedVideo = featured,
            CurrentCategory = string.IsNullOrWhiteSpace(category) ? "All" : category,
            SearchQuery = search,
            SortBy = sort ?? "featured",
            CategoryCounts = categoryCounts,
            TotalVideos = list.Count
        };

        return Task.FromResult(viewModel);
    }

    public Task<VideoItemViewModel?> GetVideoByIdAsync(string id)
    {
        var video = AllVideos.FirstOrDefault(v => v.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
        if (video != null)
        {
            // Populate related videos from same category or fallback to other high-view videos
            var related = AllVideos
                .Where(v => v.Id != video.Id && v.Category.Equals(video.Category, StringComparison.OrdinalIgnoreCase))
                .Take(4)
                .ToList();

            if (related.Count < 4)
            {
                var additional = AllVideos
                    .Where(v => v.Id != video.Id && !related.Any(r => r.Id == v.Id))
                    .Take(4 - related.Count)
                    .ToList();
                related.AddRange(additional);
            }

            video.RelatedVideos = related;
        }

        return Task.FromResult(video);
    }

    public Task<List<VideoItemViewModel>> GetRelatedVideosAsync(string id, int count = 4)
    {
        var current = AllVideos.FirstOrDefault(v => v.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
        var related = AllVideos
            .Where(v => v.Id != id && (current == null || v.Category.Equals(current.Category, StringComparison.OrdinalIgnoreCase)))
            .Take(count)
            .ToList();

        if (related.Count < count)
        {
            var additional = AllVideos
                .Where(v => v.Id != id && !related.Any(r => r.Id == v.Id))
                .Take(count - related.Count)
                .ToList();
            related.AddRange(additional);
        }

        return Task.FromResult(related);
    }
}
