namespace StartupConnect.Infrastructure;

/// <summary>
/// The platform's audience is in India: dates entered in forms (e.g. challenge deadlines) are IST and
/// stored as UTC. India has no DST, so a fixed +05:30 offset is exact.
/// </summary>
public static class AppTime
{
    public static readonly TimeSpan IstOffset = TimeSpan.FromHours(5.5);

    public static DateTime ToIst(DateTime utc) =>
        DateTime.SpecifyKind(DateTime.SpecifyKind(utc, DateTimeKind.Utc) + IstOffset, DateTimeKind.Unspecified);

    public static DateTime IstToUtc(DateTime ist) =>
        DateTime.SpecifyKind(DateTime.SpecifyKind(ist, DateTimeKind.Unspecified) - IstOffset, DateTimeKind.Utc);

    /// <summary>e.g. "Oct 12, 2026 · 11:59 PM IST".</summary>
    public static string FormatIst(DateTime utc, string format = "MMM dd, yyyy · h:mm tt") => ToIst(utc).ToString(format) + " IST";

    /// <summary>ISO-8601 UTC string for client-side countdowns.</summary>
    public static string ToIsoUtc(DateTime utc) => DateTime.SpecifyKind(utc, DateTimeKind.Utc).ToString("o");

    /// <summary>"3 days left", "5 hours left", "Closed".</summary>
    public static string TimeLeft(DateTime deadlineUtc, DateTime nowUtc)
    {
        var left = deadlineUtc - nowUtc;
        if (left <= TimeSpan.Zero) return "Closed";
        if (left.TotalDays >= 2) return $"{(int)left.TotalDays} days left";
        if (left.TotalHours >= 2) return $"{(int)left.TotalHours} hours left";
        var minutes = Math.Max(1, (int)left.TotalMinutes);
        return $"{minutes} minute{(minutes == 1 ? "" : "s")} left";
    }

    /// <summary>"just now", "5 min ago", "3 h ago", "2 d ago", else a date.</summary>
    public static string Ago(DateTime utc, DateTime nowUtc)
    {
        var d = nowUtc - utc;
        if (d.TotalMinutes < 1) return "just now";
        if (d.TotalHours < 1) return $"{(int)d.TotalMinutes} min ago";
        if (d.TotalDays < 1) return $"{(int)d.TotalHours} h ago";
        if (d.TotalDays < 7) return $"{(int)d.TotalDays} d ago";
        return ToIst(utc).ToString("MMM dd, yyyy");
    }
}
