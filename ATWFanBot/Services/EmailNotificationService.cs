using ATWFanBot.Configuration;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using Serilog;

namespace ATWFanBot.Services;

public class EmailNotificationService
{
    private readonly EmailSettings _emailSettings;
    private readonly Secrets _secrets;

    public EmailNotificationService(EmailSettings emailSettings, Secrets secrets)
    {
        _emailSettings = emailSettings;
        _secrets = secrets;
    }

    public async Task SendFailureNotificationAsync(
        string date,
        string title,
        List<(DateTime timestamp, string error)> retryHistory,
        string? dailyFilePath = null,
        string? contentPreview = null)
    {
        if (!_emailSettings.EnableNotifications)
        {
            Log.Information("Email notifications are disabled, skipping notification");
            return;
        }

        Log.Information("Sending failure notification email to {Email}", _secrets.NotificationEmail);

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_emailSettings.SenderName, _secrets.SmtpUsername));
        message.To.Add(new MailboxAddress("", _secrets.NotificationEmail));
        message.Subject = $"[ATWFanBot] Post Failed - {date}";

        var bodyBuilder = new BodyBuilder();
        bodyBuilder.TextBody = BuildFailureEmailBody(date, title, retryHistory, dailyFilePath, contentPreview);
        message.Body = bodyBuilder.ToMessageBody();

        await SendEmailWithRetryAsync(message);
    }

    private string BuildFailureEmailBody(
        string date,
        string title,
        List<(DateTime timestamp, string error)> retryHistory,
        string? dailyFilePath,
        string? contentPreview)
    {
        var sb = new System.Text.StringBuilder();

        sb.AppendLine("ATWFanBot failed to post today's content after all retry attempts.");
        sb.AppendLine();
        sb.AppendLine($"Date: {date}");
        sb.AppendLine($"Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"Title: {title}");
        sb.AppendLine();
        sb.AppendLine("Error Summary:");
        sb.AppendLine(retryHistory.LastOrDefault().error ?? "Unknown error");
        sb.AppendLine();
        sb.AppendLine("Retry History:");

        for (int i = 0; i < retryHistory.Count; i++)
        {
            var (timestamp, error) = retryHistory[i];
            sb.AppendLine($"Attempt {i + 1} ({timestamp:HH:mm:ss}): {error}");
        }

        sb.AppendLine();

        if (!string.IsNullOrEmpty(dailyFilePath))
        {
            sb.AppendLine($"Daily File: {dailyFilePath}");
        }

        if (!string.IsNullOrEmpty(contentPreview))
        {
            sb.AppendLine();
            sb.AppendLine("Content Preview:");
            sb.AppendLine(contentPreview.Length > 200
                ? contentPreview.Substring(0, 200) + "..."
                : contentPreview);
        }

        sb.AppendLine();
        sb.AppendLine("Manual Recovery Steps:");
        sb.AppendLine("1. Check if daily file exists and is readable");
        sb.AppendLine("2. Verify Reddit credentials are valid");
        sb.AppendLine("3. Check Reddit status: https://www.redditstatus.com/");
        sb.AppendLine("4. Check internet connectivity");
        sb.AppendLine("5. Review application logs for detailed error information");
        sb.AppendLine("6. If needed, manually post using Reddit web interface");
        sb.AppendLine("7. Update PostHistory.json to mark date as posted if you post manually");
        sb.AppendLine();
        sb.AppendLine("Next Scheduled Run: Tomorrow at 9:00 AM Eastern Time");
        sb.AppendLine();
        sb.AppendLine("--");
        sb.AppendLine("ATWFanBot v1.0");

        return sb.ToString();
    }

    private async Task SendEmailWithRetryAsync(MimeMessage message)
    {
        const int maxRetries = 3;
        const int delaySeconds = 30;

        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                using var client = new SmtpClient();

                await client.ConnectAsync(_emailSettings.SmtpHost, _emailSettings.SmtpPort,
                    _emailSettings.UseSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.None);

                await client.AuthenticateAsync(_secrets.SmtpUsername, _secrets.SmtpPassword);
                await client.SendAsync(message);
                await client.DisconnectAsync(true);

                Log.Information("Successfully sent email notification");
                return;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to send email (attempt {Attempt}/{MaxRetries})", attempt, maxRetries);

                if (attempt < maxRetries)
                {
                    await Task.Delay(TimeSpan.FromSeconds(delaySeconds));
                }
                else
                {
                    Log.Error(ex, "Failed to send email after {MaxRetries} attempts", maxRetries);
                    // Don't throw - we don't want email failures to crash the app
                }
            }
        }
    }

    public async Task SendTestEmailAsync()
    {
        Log.Information("Sending test email to {Email}", _secrets.NotificationEmail);

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_emailSettings.SenderName, _secrets.SmtpUsername));
        message.To.Add(new MailboxAddress("", _secrets.NotificationEmail));
        message.Subject = "[ATWFanBot] Test Email";

        var bodyBuilder = new BodyBuilder
        {
            TextBody = "This is a test email from ATWFanBot.\n\n" +
                       "If you received this, your email configuration is working correctly.\n\n" +
                       $"Sent at: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n\n" +
                       "--\nATWFanBot v1.0"
        };

        message.Body = bodyBuilder.ToMessageBody();

        await SendEmailWithRetryAsync(message);
    }
}
