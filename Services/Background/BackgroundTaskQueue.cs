using System.Threading.Channels;

namespace StartupConnect.Services.Background;

/// <summary>
/// A unit of background work. It receives a fresh DI scope's service provider (never the
/// request's) so it can safely resolve scoped services such as ApplicationDbContext.
/// </summary>
public delegate Task BackgroundWorkItem(IServiceProvider services, CancellationToken cancellationToken);

public interface IBackgroundTaskQueue
{
    /// <summary>Queues work to run after the current request has finished.</summary>
    ValueTask QueueAsync(string name, BackgroundWorkItem workItem, CancellationToken cancellationToken = default);

    ValueTask<(string Name, BackgroundWorkItem WorkItem)> DequeueAsync(CancellationToken cancellationToken);
}

/// <summary>Bounded, Channel-based in-memory work queue drained by <see cref="QueuedHostedService"/>.</summary>
public sealed class BackgroundTaskQueue : IBackgroundTaskQueue
{
    private readonly Channel<(string, BackgroundWorkItem)> _queue;

    public BackgroundTaskQueue(int capacity = 200)
    {
        _queue = Channel.CreateBounded<(string, BackgroundWorkItem)>(new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true
        });
    }

    public ValueTask QueueAsync(string name, BackgroundWorkItem workItem, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workItem);
        return _queue.Writer.WriteAsync((name, workItem), cancellationToken);
    }

    public ValueTask<(string Name, BackgroundWorkItem WorkItem)> DequeueAsync(CancellationToken cancellationToken)
        => _queue.Reader.ReadAsync(cancellationToken);
}

/// <summary>Runs queued work items one at a time, each inside its own DI scope.</summary>
public sealed class QueuedHostedService : BackgroundService
{
    private readonly IBackgroundTaskQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<QueuedHostedService> _logger;

    public QueuedHostedService(IBackgroundTaskQueue queue, IServiceScopeFactory scopeFactory, ILogger<QueuedHostedService> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            (string Name, BackgroundWorkItem WorkItem) item;
            try
            {
                item = await _queue.DequeueAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                await item.WorkItem(scope.ServiceProvider, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Background work item '{WorkItem}' failed", item.Name);
            }
        }
    }
}
