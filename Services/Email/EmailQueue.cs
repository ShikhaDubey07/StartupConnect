using StartupConnect.Services.Background;

namespace StartupConnect.Services.Email;

/// <summary>
/// Fire-and-forget email delivery: messages are sent from the background queue so a slow or
/// unavailable mail server never delays (or fails) the web request, and response times don't
/// reveal whether an email was actually sent (e.g. password reset for unknown addresses).
/// </summary>
public interface IEmailQueue
{
    Task QueueAsync(EmailMessage message, string reason);
}

public sealed class EmailQueue : IEmailQueue
{
    private static readonly TimeSpan EnqueueTimeout = TimeSpan.FromSeconds(2);
    private readonly IBackgroundTaskQueue _queue;
    private readonly ILogger<EmailQueue> _logger;

    public EmailQueue(IBackgroundTaskQueue queue, ILogger<EmailQueue> logger)
    {
        _queue = queue;
        _logger = logger;
    }

    public async Task QueueAsync(EmailMessage message, string reason)
    {
        try
        {
            using var cts = new CancellationTokenSource(EnqueueTimeout);
            await _queue.QueueAsync($"email:{reason}", async (services, ct) =>
            {
                var sender = services.GetRequiredService<IEmailSender>();
                await sender.SendAsync(message, ct);
            }, cts.Token);
        }
        catch (OperationCanceledException)
        {
            _logger.LogError("Background queue is full — dropped '{Reason}' email '{Subject}'", reason, message.Subject);
        }
    }
}
