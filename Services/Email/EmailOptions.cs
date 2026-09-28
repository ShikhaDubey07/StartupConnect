namespace StartupConnect.Services.Email;

/// <summary>Bound from the "Email" configuration section.</summary>
public class EmailOptions
{
    public const string SectionName = "Email";

    public string FromAddress { get; set; } = "no-reply@startupconnect.in";
    public string FromName { get; set; } = "StartupConnect";

    /// <summary>
    /// Force (true) or disable (false) the dev outbox. When unset, the outbox is used in Development
    /// or whenever no SMTP host is configured.
    /// </summary>
    public bool? UseDevOutbox { get; set; }

    /// <summary>Folder (relative to the content root unless absolute) where the dev outbox stores emails.</summary>
    public string OutboxPath { get; set; } = "App_Data/outbox";

    public SmtpOptions Smtp { get; set; } = new();
}

public class SmtpOptions
{
    public string? Host { get; set; }
    public int Port { get; set; } = 587;
    public string? Username { get; set; }
    public string? Password { get; set; }
    public bool EnableSsl { get; set; } = true;
}
