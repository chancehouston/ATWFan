namespace ATWFanBot.Configuration;

public class AppSettings
{
    public RedditSettings Reddit { get; set; } = new();
    public PostingSettings Posting { get; set; } = new();
    public RetrySettings Retry { get; set; } = new();
    public EmailSettings Email { get; set; } = new();
    public LoggingSettings Logging { get; set; } = new();
}

public class RedditSettings
{
    public string Subreddit { get; set; } = "AdamTheWoo";
    public string PostTitleTemplate { get; set; } = "On This Day - {0} - Adam The Woo's Adventures";
    public string UserAgent { get; set; } = "windows:ATWFanBot:v1.0";
}

public class PostingSettings
{
    public string TimeZone { get; set; } = "Eastern Standard Time";
    public int PostHour { get; set; } = 9;
    public int PostMinute { get; set; } = 0;
    public string DailyFolderPath { get; set; } = "../Daily";
    public string HistoryFilePath { get; set; } = "%LOCALAPPDATA%/ATWFanBot/PostHistory.json";
}

public class RetrySettings
{
    public int MaxRetries { get; set; } = 3;
    public int DelayMinutes { get; set; } = 10;
}

public class EmailSettings
{
    public string SmtpHost { get; set; } = "smtp.gmail.com";
    public int SmtpPort { get; set; } = 587;
    public bool UseSsl { get; set; } = true;
    public string SenderName { get; set; } = "ATWFanBot";
    public bool EnableNotifications { get; set; } = true;
}

public class LoggingSettings
{
    public string LogDirectory { get; set; } = "%LOCALAPPDATA%/ATWFanBot/Logs";
    public int RetainDays { get; set; } = 30;
}
