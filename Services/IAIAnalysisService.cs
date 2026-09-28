using StartupConnect.Models;

namespace StartupConnect.Services;

public interface IAIAnalysisService
{
    /// <summary>True when GoogleGemini:ApiKey is set. Callers should skip AI features gracefully otherwise.</summary>
    bool IsConfigured { get; }

    /// <summary>Calls Gemini and upserts the IdeaAnalysis row. Throws <see cref="AiServiceException"/> on failure.</summary>
    Task<IdeaAnalysis> AnalyzeIdeaAsync(int ideaId);
    Task<IdeaAnalysis> GenerateMockAnalysisAsync(Idea idea);

    /// <summary>
    /// Returns a one-sentence AI rationale explaining why userId1 and userId2 are a good co-founder match.
    /// Result is cached in CoFounderMatchInsights — Gemini is only called if no cache entry exists.
    /// </summary>
    Task<string> GetCoFounderRationaleAsync(string userId1, string userId2);

    /// <summary>
    /// Generates and caches a 2-sentence investor-facing pitch for the given idea.
    /// Uses the stored IdeaAnalysis.Summary + MarketPotential as input — no raw idea text is re-sent.
    /// Returns null if no IdeaAnalysis exists yet for the idea.
    /// </summary>
    Task<string?> GenerateInvestorPitchAsync(int ideaId);
}
