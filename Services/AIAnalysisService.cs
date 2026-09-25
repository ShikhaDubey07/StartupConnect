using Microsoft.EntityFrameworkCore;
using StartupConnect.Data;
using StartupConnect.Models;
using System.Text;
using System.Text.Json;

namespace StartupConnect.Services;

public class AIAnalysisService : IAIAnalysisService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AIAnalysisService> _logger;
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;

    public AIAnalysisService(
        IServiceProvider serviceProvider,
        ILogger<AIAnalysisService> logger,
        HttpClient httpClient,
        IConfiguration configuration)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _httpClient = httpClient;
        _configuration = configuration;
    }

    public async Task<IdeaAnalysis> AnalyzeIdeaAsync(int ideaId)
    {
        using var scope = _serviceProvider.CreateScope();

        var context = scope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>();

        var idea = await context.Ideas
            .Include(i => i.Category)
            .FirstOrDefaultAsync(i => i.Id == ideaId);

        if (idea == null)
            throw new Exception("Idea not found.");

        _logger.LogInformation(
            "Starting AI analysis for Idea {IdeaId}", ideaId);

        var apiKey = _configuration["GoogleGemini:ApiKey"];

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException(
                "Gemini API key is missing. Add GoogleGemini:ApiKey to appsettings.json.");
        }

        var analysis = await GenerateRealAnalysisAsync(idea, apiKey);

        var existing = await context.IdeaAnalyses
            .FirstOrDefaultAsync(a => a.IdeaId == ideaId);

        if (existing != null)
        {
            existing.Summary = analysis.Summary;
            existing.ProblemsAndSolutions = analysis.ProblemsAndSolutions;
            existing.TargetUsers = analysis.TargetUsers;
            existing.MarketPotential = analysis.MarketPotential;
            existing.Risks = analysis.Risks;
            existing.RevenueModels = analysis.RevenueModels;
            existing.TeamSuggestions = analysis.TeamSuggestions;
            existing.OverallScore = analysis.OverallScore;
            existing.SimilarIdeasJson = analysis.SimilarIdeasJson;
            existing.GeneratedAt = DateTime.UtcNow;
        }
        else
        {
            context.IdeaAnalyses.Add(analysis);
        }

        await context.SaveChangesAsync();

        _logger.LogInformation(
            "Completed AI analysis for Idea {IdeaId}", ideaId);

        return existing ?? analysis;
    }

    private async Task<IdeaAnalysis> GenerateRealAnalysisAsync(
        Idea idea,
        string apiKey)
    {
        const string model = "gemini-3.6-flash";

        var url =
            $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent";

        var prompt = $$"""
        Analyze the following startup idea carefully.

        Return ONLY valid JSON.
        Do not use markdown.
        Do not use ```json.
        Do not add any explanation outside JSON.

        JSON structure:

        {
          "summary": "Brief executive summary of the startup idea.",
          "problemsAndSolutions": "Explain the problem and how the proposed solution solves it.",
          "targetUsers": "Explain target customers and users.",
          "marketPotential": "Explain market opportunity, demand and growth potential.",
          "risks": "Explain important risks, weaknesses and challenges.",
          "revenueModels": "Suggest suitable revenue models.",
          "teamSuggestions": "Suggest required team roles and improvements.",
          "overallScore": 75,
          "similarIdeas": [
            {
              "title": "Similar startup/product",
              "matchPercentage": 70,
              "detail": "Explain why it is similar."
            }
          ]
        }

        Startup Idea:

        Title:
        {{idea.Title}}

        Tagline:
        {{idea.Tagline}}

        Description:
        {{idea.Description}}

        Problem Statement:
        {{idea.ProblemStatement}}

        Solution:
        {{idea.Solution}}

        Target Market:
        {{idea.TargetMarket}}

        Business Model:
        {{idea.BusinessModel}}

        Category:
        {{idea.Category?.Name ?? "Uncategorized"}}

        Minimum Fund Required:
        {{idea.MinimumFundRequired}}

        Expected Team Size:
        {{idea.ExpectedTeamSize}}

        Give practical startup-focused analysis.
        Include useful suggestions for improving the idea.
        """;

        var requestBody = new
        {
            contents = new[]
            {
                new
                {
                    parts = new[]
                    {
                        new
                        {
                            text = prompt
                        }
                    }
                }
            },
            generationConfig = new
            {
                responseMimeType = "application/json",
                temperature = 0.4
            }
        };

        var json = JsonSerializer.Serialize(requestBody);

        using var content = new StringContent(
            json,
            Encoding.UTF8,
            "application/json");

        _logger.LogInformation(
            "Calling Gemini model {Model}", model);

        var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Add("x-goog-api-key", apiKey);
        request.Content = content;

        var response = await _httpClient.SendAsync(request);

        var responseString =
            await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError(
                "Gemini API error. Status: {StatusCode}, Response: {Response}",
                response.StatusCode,
                responseString);

            throw new Exception(
                $"Gemini API error: {response.StatusCode}. " +
                "Check API key, model availability and API configuration.");
        }

        using var jsonDocument =
            JsonDocument.Parse(responseString);

        var candidates =
            jsonDocument.RootElement.GetProperty("candidates");

        if (candidates.GetArrayLength() == 0)
        {
            throw new Exception(
                "Gemini returned no candidates.");
        }

        var textResult =
            candidates[0]
                .GetProperty("content")
                .GetProperty("parts")[0]
                .GetProperty("text")
                .GetString();

        if (string.IsNullOrWhiteSpace(textResult))
        {
            throw new Exception(
                "Gemini returned an empty response.");
        }

        // Remove accidental markdown if model returns it
        textResult = textResult
            .Replace("```json", "")
            .Replace("```", "")
            .Trim();

        using var resultJson =
            JsonDocument.Parse(textResult);

        var root = resultJson.RootElement;

        return new IdeaAnalysis
        {
            IdeaId = idea.Id,

            Summary =
                root.GetProperty("summary")
                    .GetString() ?? "",

            ProblemsAndSolutions =
                root.GetProperty("problemsAndSolutions")
                    .GetString() ?? "",

            TargetUsers =
                root.GetProperty("targetUsers")
                    .GetString() ?? "",

            MarketPotential =
                root.GetProperty("marketPotential")
                    .GetString() ?? "",

            Risks =
                root.GetProperty("risks")
                    .GetString() ?? "",

            RevenueModels =
                root.GetProperty("revenueModels")
                    .GetString() ?? "",

            TeamSuggestions =
                root.GetProperty("teamSuggestions")
                    .GetString() ?? "",

            OverallScore =
                root.GetProperty("overallScore")
                    .GetInt32(),

            SimilarIdeasJson =
                root.GetProperty("similarIdeas")
                    .GetRawText(),

            GeneratedAt = DateTime.UtcNow
        };
    }

    public async Task<IdeaAnalysis> GenerateMockAnalysisAsync(Idea idea)
    {
        await Task.Delay(100);

        return new IdeaAnalysis
        {
            IdeaId = idea.Id,
            GeneratedAt = DateTime.UtcNow
        };
    }

    /// <inheritdoc/>
    public async Task<string> GetCoFounderRationaleAsync(string userId1, string userId2)
    {
        // Normalise pair order so (A,B) and (B,A) resolve to the same cache row
        var (key1, key2) = string.Compare(userId1, userId2, StringComparison.Ordinal) <= 0
            ? (userId1, userId2)
            : (userId2, userId1);

        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // --- Cache-first lookup ---
        var cached = await context.CoFounderMatchInsights
            .FirstOrDefaultAsync(c => c.UserId1 == key1 && c.UserId2 == key2);

        if (cached != null)
            return cached.Rationale;

        // --- Load the two profiles for prompt construction ---
        var profiles = await context.UserProfiles
            .Include(p => p.User)
            .Include(p => p.Skills)
            .Include(p => p.InterestTags).ThenInclude(t => t.Category)
            .Where(p => p.UserId == userId1 || p.UserId == userId2)
            .ToListAsync();

        var profileA = profiles.FirstOrDefault(p => p.UserId == userId1);
        var profileB = profiles.FirstOrDefault(p => p.UserId == userId2);

        if (profileA == null || profileB == null)
            return "These profiles complement each other well.";

        // Build a compact prompt — ~80 input tokens
        var skillsA = string.Join(", ", profileA.Skills.Select(s => s.SkillName).Take(5));
        var interestsA = string.Join(", ", profileA.InterestTags.Select(t => t.Category.Name).Take(4));
        var skillsB = string.Join(", ", profileB.Skills.Select(s => s.SkillName).Take(5));
        var interestsB = string.Join(", ", profileB.InterestTags.Select(t => t.Category.Name).Take(4));
        var roleA = profileA.IsInvestor ? "Investor" : "Founder";
        var roleB = profileB.IsInvestor ? "Investor" : "Founder";

        var prompt =
            $"Person A: Role={roleA}, Skills=[{skillsA}], Interests=[{interestsA}], City={profileA.User.City ?? "unknown"}\n" +
            $"Person B: Role={roleB}, Skills=[{skillsB}], Interests=[{interestsB}], City={profileB.User.City ?? "unknown"}\n" +
            "Write exactly one sentence explaining why they are a strong co-founder match. Be specific about the complementary skills or shared domain. Return only the sentence with no extra text.";

        var rationale = await CallGeminiLiteAsync(prompt, maxOutputTokens: 80);

        // --- Persist to cache ---
        context.CoFounderMatchInsights.Add(new CoFounderMatchInsight
        {
            UserId1 = key1,
            UserId2 = key2,
            Rationale = rationale,
            GeneratedAt = DateTime.UtcNow
        });

        try { await context.SaveChangesAsync(); }
        catch (Exception ex)
        {
            // Race condition: another request may have inserted first — that's fine
            _logger.LogWarning("CoFounderMatchInsight insert race: {Message}", ex.Message);
        }

        return rationale;
    }

    /// <inheritdoc/>
    public async Task<string?> GenerateInvestorPitchAsync(int ideaId)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var analysis = await context.IdeaAnalyses.FirstOrDefaultAsync(a => a.IdeaId == ideaId);
        if (analysis == null) return null;

        // --- Cache-first lookup ---
        if (!string.IsNullOrWhiteSpace(analysis.InvestorPitchSummary))
            return analysis.InvestorPitchSummary;

        // Use already-stored fields — do NOT re-send the raw idea text
        if (string.IsNullOrWhiteSpace(analysis.Summary) && string.IsNullOrWhiteSpace(analysis.MarketPotential))
            return null;

        var prompt =
            $"Startup summary: {analysis.Summary}\n" +
            $"Market potential: {analysis.MarketPotential}\n" +
            "Write exactly 2 concise sentences that pitch this startup to an investor. " +
            "Focus on market opportunity and traction potential. Return only the 2 sentences with no extra text.";

        var pitch = await CallGeminiLiteAsync(prompt, maxOutputTokens: 120);

        // --- Cache result in the existing analysis row ---
        analysis.InvestorPitchSummary = pitch;
        await context.SaveChangesAsync();

        return pitch;
    }

    /// <summary>
    /// Shared helper that calls gemini-2.0-flash-lite with plain-text output.
    /// Enforces maxOutputTokens and temperature=0.3 for efficiency and consistency.
    /// </summary>
    private async Task<string> CallGeminiLiteAsync(string prompt, int maxOutputTokens)
    {
        const string model = "gemini-3.6-flash";
        var apiKey = _configuration["GoogleGemini:ApiKey"]
            ?? throw new InvalidOperationException("Gemini API key missing.");

        var url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent";

        var requestBody = new
        {
            contents = new[]
            {
                new { parts = new[] { new { text = prompt } } }
            },
            generationConfig = new
            {
                temperature = 0.3,
                maxOutputTokens
            }
        };

        var json = JsonSerializer.Serialize(requestBody);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");

        var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Add("x-goog-api-key", apiKey);
        request.Content = content;

        var response = await _httpClient.SendAsync(request);
        var responseString = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Gemini Lite API error. Status: {Status}, Body: {Body}",
                response.StatusCode, responseString);
            throw new Exception($"Gemini Lite API error: {response.StatusCode}");
        }

        using var doc = JsonDocument.Parse(responseString);
        var text = doc.RootElement
            .GetProperty("candidates")[0]
            .GetProperty("content")
            .GetProperty("parts")[0]
            .GetProperty("text")
            .GetString() ?? string.Empty;

        return text.Trim();
    }
}