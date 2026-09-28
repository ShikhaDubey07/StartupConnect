using System.Collections.Concurrent;
using StartupConnect.Services.Background;

namespace StartupConnect.Services;

public enum AnalysisJobState
{
    Queued,
    Running,
    Failed
}

public sealed record AnalysisJobStatus(AnalysisJobState State, string? Message, DateTime UpdatedAtUtc);

/// <summary>
/// In-memory status of AI analysis jobs, keyed by idea id. Completed jobs are removed (the
/// IdeaAnalysis row is then the source of truth); failed jobs keep a user-friendly message.
/// </summary>
public interface IAnalysisJobTracker
{
    AnalysisJobStatus? Get(int ideaId);
    /// <summary>Marks the idea as queued. Returns false if a job is already queued or running.</summary>
    bool TryMarkQueued(int ideaId);
    void MarkRunning(int ideaId);
    void MarkFailed(int ideaId, string friendlyMessage);
    void Clear(int ideaId);
}

public sealed class AnalysisJobTracker : IAnalysisJobTracker
{
    private readonly ConcurrentDictionary<int, AnalysisJobStatus> _jobs = new();

    public AnalysisJobStatus? Get(int ideaId) => _jobs.TryGetValue(ideaId, out var s) ? s : null;

    public bool TryMarkQueued(int ideaId)
    {
        var queued = new AnalysisJobStatus(AnalysisJobState.Queued, null, DateTime.UtcNow);
        while (true)
        {
            if (!_jobs.TryGetValue(ideaId, out var current))
            {
                if (_jobs.TryAdd(ideaId, queued)) return true;
                continue;
            }

            // A job stuck for over 10 minutes (e.g. the worker crashed) may be replaced.
            var stale = DateTime.UtcNow - current.UpdatedAtUtc > TimeSpan.FromMinutes(10);
            if (current.State != AnalysisJobState.Failed && !stale) return false;
            if (_jobs.TryUpdate(ideaId, queued, current)) return true;
        }
    }

    public void MarkRunning(int ideaId) =>
        _jobs[ideaId] = new AnalysisJobStatus(AnalysisJobState.Running, null, DateTime.UtcNow);

    public void MarkFailed(int ideaId, string friendlyMessage) =>
        _jobs[ideaId] = new AnalysisJobStatus(AnalysisJobState.Failed, friendlyMessage, DateTime.UtcNow);

    public void Clear(int ideaId) => _jobs.TryRemove(ideaId, out _);
}

public enum AnalysisEnqueueResult
{
    Queued,
    AlreadyInProgress
}

public interface IIdeaAnalysisScheduler
{
    /// <summary>Queues a Gemini analysis for the idea on the background worker.</summary>
    Task<AnalysisEnqueueResult> EnqueueAsync(int ideaId, CancellationToken cancellationToken = default);
}

public sealed class IdeaAnalysisScheduler : IIdeaAnalysisScheduler
{
    private readonly IBackgroundTaskQueue _queue;
    private readonly IAnalysisJobTracker _tracker;

    public IdeaAnalysisScheduler(IBackgroundTaskQueue queue, IAnalysisJobTracker tracker)
    {
        _queue = queue;
        _tracker = tracker;
    }

    public async Task<AnalysisEnqueueResult> EnqueueAsync(int ideaId, CancellationToken cancellationToken = default)
    {
        if (!_tracker.TryMarkQueued(ideaId)) return AnalysisEnqueueResult.AlreadyInProgress;

        await _queue.QueueAsync($"ai-analysis:{ideaId}", async (services, ct) =>
        {
            var tracker = services.GetRequiredService<IAnalysisJobTracker>();
            var ai = services.GetRequiredService<IAIAnalysisService>();
            var logger = services.GetRequiredService<ILogger<IdeaAnalysisScheduler>>();

            if (!ai.IsConfigured)
            {
                logger.LogWarning("Skipping AI analysis for idea {IdeaId}: GoogleGemini:ApiKey is not configured", ideaId);
                tracker.MarkFailed(ideaId, AiMessages.NotConfigured);
                return;
            }

            tracker.MarkRunning(ideaId);
            try
            {
                await ai.AnalyzeIdeaAsync(ideaId);
                tracker.Clear(ideaId);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "AI analysis failed for idea {IdeaId}", ideaId);
                tracker.MarkFailed(ideaId, ex is AiServiceException ase ? ase.FriendlyMessage : AiMessages.GenericFailure);
            }
        }, cancellationToken);

        return AnalysisEnqueueResult.Queued;
    }
}

public static class AiMessages
{
    public const string NotConfigured =
        "AI analysis isn't available on this server right now. Your idea is saved and reviewers will still see it — you can come back later for AI insights.";

    public const string GenericFailure =
        "We couldn't generate the AI analysis this time. Please try again in a few minutes.";
}

/// <summary>An AI failure carrying a message that is safe to show to end users.</summary>
public class AiServiceException : Exception
{
    public string FriendlyMessage { get; }

    public AiServiceException(string friendlyMessage, string? technicalMessage = null, Exception? inner = null)
        : base(technicalMessage ?? friendlyMessage, inner)
    {
        FriendlyMessage = friendlyMessage;
    }
}

public sealed class AiNotConfiguredException : AiServiceException
{
    public AiNotConfiguredException() : base(AiMessages.NotConfigured, "GoogleGemini:ApiKey is not configured.") { }
}
