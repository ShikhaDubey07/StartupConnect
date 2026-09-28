using System.Text;
using System.Text.Encodings.Web;

namespace StartupConnect.Services.Email;

/// <summary>
/// Builds branded transactional emails. All inputs are plain text and are HTML-encoded here, so
/// user-controlled values (names, idea titles) can be passed safely.
/// </summary>
public static class EmailTemplates
{
    /// <summary>Generic branded email with an optional call-to-action button.</summary>
    public static EmailMessage Build(
        string to,
        string subject,
        string greeting,
        IEnumerable<string> paragraphs,
        string? buttonText = null,
        string? buttonUrl = null,
        string? footnote = null)
    {
        var enc = HtmlEncoder.Default;
        var paras = paragraphs.ToList();

        var html = new StringBuilder();
        html.Append("<!DOCTYPE html><html><body style=\"margin:0;padding:0;background:#f4f5f7;font-family:Inter,Segoe UI,Arial,sans-serif;color:#1f2937\">");
        html.Append("<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" style=\"background:#f4f5f7;padding:32px 12px\"><tr><td align=\"center\">");
        html.Append("<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" style=\"max-width:560px;background:#ffffff;border-radius:16px;overflow:hidden\">");
        html.Append("<tr><td style=\"background:#1f2937;padding:20px 28px;color:#ffffff;font-size:18px;font-weight:700\">&#128640; StartupConnect</td></tr>");
        html.Append("<tr><td style=\"padding:28px\">");
        html.Append($"<p style=\"font-size:16px;font-weight:600;margin:0 0 16px\">{enc.Encode(greeting)}</p>");
        foreach (var p in paras)
            html.Append($"<p style=\"font-size:15px;line-height:1.6;margin:0 0 16px\">{enc.Encode(p)}</p>");
        if (!string.IsNullOrEmpty(buttonText) && !string.IsNullOrEmpty(buttonUrl))
        {
            html.Append($"<p style=\"margin:24px 0\"><a href=\"{enc.Encode(buttonUrl)}\" style=\"display:inline-block;background:#b87333;color:#ffffff;text-decoration:none;font-weight:600;padding:12px 24px;border-radius:999px\">{enc.Encode(buttonText)}</a></p>");
            html.Append($"<p style=\"font-size:13px;color:#6b7280;margin:0 0 16px\">If the button doesn't work, copy this link into your browser:<br><a href=\"{enc.Encode(buttonUrl)}\" style=\"color:#6b7280;word-break:break-all\">{enc.Encode(buttonUrl)}</a></p>");
        }
        if (!string.IsNullOrEmpty(footnote))
            html.Append($"<p style=\"font-size:13px;color:#6b7280;margin:16px 0 0\">{enc.Encode(footnote)}</p>");
        html.Append("</td></tr><tr><td style=\"padding:16px 28px;background:#f9fafb;font-size:12px;color:#9ca3af\">You received this email because of activity on your StartupConnect account.</td></tr>");
        html.Append("</table></td></tr></table></body></html>");

        var text = new StringBuilder();
        text.AppendLine(greeting).AppendLine();
        foreach (var p in paras) text.AppendLine(p).AppendLine();
        if (!string.IsNullOrEmpty(buttonText) && !string.IsNullOrEmpty(buttonUrl))
            text.AppendLine($"{buttonText}: {buttonUrl}").AppendLine();
        if (!string.IsNullOrEmpty(footnote)) text.AppendLine(footnote);
        text.AppendLine().Append("— The StartupConnect team");

        return new EmailMessage(to, subject, html.ToString(), text.ToString());
    }

    public static EmailMessage ConfirmEmail(string to, string fullName, string confirmUrl) => Build(
        to,
        "Confirm your StartupConnect email",
        $"Hi {FirstName(fullName)},",
        new[]
        {
            "Welcome to StartupConnect! Please confirm your email address so you can submit ideas and connect with founders and investors.",
        },
        "Confirm my email",
        confirmUrl,
        "This link expires in 24 hours. If you didn't create a StartupConnect account, you can ignore this email.");

    private static string FirstName(string fullName)
    {
        var first = (fullName ?? string.Empty).Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        return string.IsNullOrEmpty(first) ? "there" : first;
    }
}
