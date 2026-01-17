namespace ATWFanBot.Configuration;

public class Secrets
{
    public string RedditClientId { get; set; } = string.Empty;
    public string RedditClientSecret { get; set; } = string.Empty;
    public string RedditUsername { get; set; } = string.Empty;
    public string RedditPassword { get; set; } = string.Empty;
    public string SmtpUsername { get; set; } = string.Empty;
    public string SmtpPassword { get; set; } = string.Empty;
    public string NotificationEmail { get; set; } = string.Empty;

    public void ValidateOrThrow()
    {
        var missing = new List<string>();

        if (string.IsNullOrWhiteSpace(RedditClientId))
            missing.Add("REDDIT_CLIENT_ID");
        if (string.IsNullOrWhiteSpace(RedditClientSecret))
            missing.Add("REDDIT_CLIENT_SECRET");
        if (string.IsNullOrWhiteSpace(RedditUsername))
            missing.Add("REDDIT_USERNAME");
        if (string.IsNullOrWhiteSpace(RedditPassword))
            missing.Add("REDDIT_PASSWORD");
        if (string.IsNullOrWhiteSpace(SmtpUsername))
            missing.Add("SMTP_USERNAME");
        if (string.IsNullOrWhiteSpace(SmtpPassword))
            missing.Add("SMTP_PASSWORD");
        if (string.IsNullOrWhiteSpace(NotificationEmail))
            missing.Add("NOTIFICATION_EMAIL");

        if (missing.Count > 0)
        {
            throw new InvalidOperationException(
                $"Missing required environment variables: {string.Join(", ", missing)}. " +
                "Please set these environment variables before running the application.");
        }
    }

    public static Secrets LoadFromEnvironment()
    {
        return new Secrets
        {
            RedditClientId = Environment.GetEnvironmentVariable("REDDIT_CLIENT_ID") ?? string.Empty,
            RedditClientSecret = Environment.GetEnvironmentVariable("REDDIT_CLIENT_SECRET") ?? string.Empty,
            RedditUsername = Environment.GetEnvironmentVariable("REDDIT_USERNAME") ?? string.Empty,
            RedditPassword = Environment.GetEnvironmentVariable("REDDIT_PASSWORD") ?? string.Empty,
            SmtpUsername = Environment.GetEnvironmentVariable("SMTP_USERNAME") ?? string.Empty,
            SmtpPassword = Environment.GetEnvironmentVariable("SMTP_PASSWORD") ?? string.Empty,
            NotificationEmail = Environment.GetEnvironmentVariable("NOTIFICATION_EMAIL") ?? string.Empty
        };
    }
}
