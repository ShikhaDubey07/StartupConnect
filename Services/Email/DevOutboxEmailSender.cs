using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;

namespace StartupConnect.Services.Email;

public sealed record OutboxEmail(string Id, string To, string Subject, string HtmlBody, string TextBody, DateTime SentAtUtc)
{
    /// <summary>Absolute links found in the HTML body (handy for clicking confirmation links in dev).</summary>
    public IReadOnlyList<string> Links =>
        Regex.Matches(HtmlBody, "href=\"(https?://[^\"]+)\"")
            .Select(m => System.Net.WebUtility.HtmlDecode(m.Groups[1].Value))
            .Distinct()
            .ToList();
}

public interface IDevOutbox
{
    IReadOnlyList<OutboxEmail> List(int max = 100);
    OutboxEmail? Get(string id);
    void Clear();
}

/// <summary>
/// Development / no-SMTP email sender: writes each email to the log and saves it as JSON under
/// <see cref="EmailOptions.OutboxPath"/> so flows like email confirmation can be completed locally.
/// </summary>
public sealed class DevOutboxEmailSender : IEmailSender, IDevOutbox
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private static readonly Regex SafeId = new("^[A-Za-z0-9-]+$", RegexOptions.Compiled);
    private readonly string _folder;
    private readonly ILogger<DevOutboxEmailSender> _logger;

    public DevOutboxEmailSender(IOptions<EmailOptions> options, IWebHostEnvironment env, ILogger<DevOutboxEmailSender> logger)
    {
        var path = options.Value.OutboxPath;
        _folder = Path.IsPathRooted(path) ? path : Path.Combine(env.ContentRootPath, path);
        _logger = logger;
    }

    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_folder);
        var id = $"{DateTime.UtcNow:yyyyMMddHHmmssfff}-{Guid.NewGuid().ToString("N")[..8]}";
        var email = new OutboxEmail(id, message.To, message.Subject, message.HtmlBody, message.TextBody, DateTime.UtcNow);
        await File.WriteAllTextAsync(Path.Combine(_folder, id + ".json"), JsonSerializer.Serialize(email, JsonOptions), cancellationToken);

        _logger.LogInformation(
            "[Dev outbox] Email to {To} | Subject: {Subject} | View it at /Dev/Outbox/{Id}\n{Body}",
            message.To, message.Subject, id, message.TextBody);
    }

    public IReadOnlyList<OutboxEmail> List(int max = 100)
    {
        if (!Directory.Exists(_folder)) return Array.Empty<OutboxEmail>();
        return Directory.EnumerateFiles(_folder, "*.json")
            .OrderByDescending(Path.GetFileName, StringComparer.Ordinal)
            .Take(max)
            .Select(Read)
            .Where(e => e != null)
            .Cast<OutboxEmail>()
            .ToList();
    }

    public OutboxEmail? Get(string id)
    {
        if (string.IsNullOrEmpty(id) || !SafeId.IsMatch(id)) return null;
        var file = Path.Combine(_folder, id + ".json");
        return File.Exists(file) ? Read(file) : null;
    }

    public void Clear()
    {
        if (!Directory.Exists(_folder)) return;
        foreach (var file in Directory.EnumerateFiles(_folder, "*.json")) File.Delete(file);
    }

    private OutboxEmail? Read(string file)
    {
        try
        {
            return JsonSerializer.Deserialize<OutboxEmail>(File.ReadAllText(file));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not read outbox email {File}", file);
            return null;
        }
    }
}
