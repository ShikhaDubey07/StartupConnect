namespace StartupConnect.Infrastructure;

public static class UrlSafety
{
    /// <summary>
    /// True only for absolute http(s) URLs. Use before rendering user-supplied links as href to
    /// prevent javascript:/data: URL injection.
    /// </summary>
    public static bool IsSafeHttpUrl(string? url) =>
        !string.IsNullOrWhiteSpace(url)
        && Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    /// <summary>True for app-relative paths like "/Ideas/Detail/5" (not "//evil.com" or "/\\evil.com").</summary>
    public static bool IsLocalPath(string? url) =>
        !string.IsNullOrEmpty(url)
        && url[0] == '/'
        && (url.Length == 1 || (url[1] != '/' && url[1] != '\\'));
}
