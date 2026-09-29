namespace StartupConnect.Infrastructure;

/// <summary>
/// Builds absolute links for emails. Prefers the configured <c>App:BaseUrl</c> (e.g.
/// "https://startupconnect.in") so links can't be poisoned through the Host header; falls back to
/// the current request's scheme/host (fine for local development).
/// </summary>
public interface IAppUrls
{
    /// <summary>Absolute URL for an app-relative path ("/Ideas/Detail/5"), or null when neither a base URL
    /// nor a current request is available (background work without App:BaseUrl).</summary>
    string? Absolute(string? relativePath);

    /// <summary>Absolute URL of the notification preferences page (used in email footers).</summary>
    string? ManagePreferencesUrl { get; }
}

public sealed class AppUrls : IAppUrls
{
    public const string ConfigKey = "App:BaseUrl";
    public const string PreferencesPath = "/Settings?tab=notifications";

    private readonly IHttpContextAccessor _http;
    private readonly string? _baseUrl;

    public AppUrls(IConfiguration configuration, IHttpContextAccessor http, ILogger<AppUrls> logger)
    {
        _http = http;
        var configured = configuration[ConfigKey]?.Trim().TrimEnd('/');
        if (!string.IsNullOrEmpty(configured))
        {
            if (UrlSafety.IsSafeHttpUrl(configured))
                _baseUrl = configured;
            else
                logger.LogWarning("{Key} '{Value}' is not an absolute http(s) URL and is ignored.", ConfigKey, configured);
        }
    }

    public string? Absolute(string? relativePath)
    {
        if (string.IsNullOrEmpty(relativePath)) relativePath = "/";
        if (UrlSafety.IsSafeHttpUrl(relativePath)) return relativePath;
        if (!UrlSafety.IsLocalPath(relativePath)) return null;

        var root = _baseUrl;
        if (root == null)
        {
            var request = _http.HttpContext?.Request;
            if (request == null || !request.Host.HasValue) return null;
            root = $"{request.Scheme}://{request.Host.Value}{request.PathBase.Value}".TrimEnd('/');
        }
        return root + relativePath;
    }

    public string? ManagePreferencesUrl => Absolute(PreferencesPath);
}
