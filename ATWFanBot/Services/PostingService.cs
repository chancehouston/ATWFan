using System.Diagnostics;
using ATWFanBot.Configuration;
using Polly;
using Polly.Retry;
using Serilog;

namespace ATWFanBot.Services;

public class PostingService
{
    private readonly AppSettings _settings;
    private readonly Secrets _secrets;
    private readonly RedditApiClient _redditClient;
    private readonly DailyFileContentProvider _contentProvider;
    private readonly PostHistoryManager _historyManager;
    private readonly EmailNotificationService _emailService;
    private readonly AsyncRetryPolicy _retryPolicy;

    public PostingService(
        AppSettings settings,
        Secrets secrets,
        RedditApiClient redditClient,
        DailyFileContentProvider contentProvider,
        PostHistoryManager historyManager,
        EmailNotificationService emailService)
    {
        _settings = settings;
        _secrets = secrets;
        _redditClient = redditClient;
        _contentProvider = contentProvider;
        _historyManager = historyManager;
        _emailService = emailService;

        // Configure Polly retry policy: 3 retries with 10-minute delay
        _retryPolicy = Policy
            .Handle<Exception>(ex => IsRetryableException(ex))
            .WaitAndRetryAsync(
                retryCount: _settings.Retry.MaxRetries,
                sleepDurationProvider: attempt => TimeSpan.FromMinutes(_settings.Retry.DelayMinutes),
                onRetry: (exception, timeSpan, retryCount, context) =>
                {
                    Log.Warning(exception,
                        "Retry {RetryCount}/{MaxRetries} after {DelayMinutes} minutes. Error: {Message}",
                        retryCount, _settings.Retry.MaxRetries, _settings.Retry.DelayMinutes, exception.Message);
                });
    }

    private bool IsRetryableException(Exception ex)
    {
        // Don't retry authentication failures or forbidden errors
        if (ex is HttpRequestException httpEx)
        {
            var message = httpEx.Message.ToLower();
            if (message.Contains("401") || message.Contains("403") || message.Contains("404"))
            {
                Log.Warning("Non-retryable HTTP error encountered: {Message}", httpEx.Message);
                return false;
            }
        }

        // Retry all other exceptions (network errors, timeouts, Reddit server errors, etc.)
        return true;
    }

    public async Task<bool> PostDailyContentAsync(string? subredditOverride = null, bool dryRun = false)
    {
        var stopwatch = Stopwatch.StartNew();
        var easternZone = TimeZoneInfo.FindSystemTimeZoneById(_settings.Posting.TimeZone);
        var easternNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, easternZone);
        var date = easternNow.ToString("MM-dd");
        var retryHistory = new List<(DateTime timestamp, string error)>();

        Log.Information("Starting daily post process for date: {Date} (Eastern Time: {EasternTime})",
            date, easternNow.ToString("yyyy-MM-dd HH:mm:ss"));

        try
        {
            // Check idempotency - have we already posted today?
            if (_historyManager.HasSuccessfulPostForDate(date))
            {
                Log.Information("Post for {Date} already exists in history. Skipping to prevent duplicate.", date);
                return true;
            }

            // Validate daily file exists and is readable
            Log.Information("Validating daily content file");
            _contentProvider.ValidateDailyFile(easternNow);

            // Get content
            var subreddit = subredditOverride ?? _settings.Reddit.Subreddit;
            var (title, body) = _contentProvider.GetDailyContent(easternNow, subreddit);

            if (dryRun)
            {
                Log.Information("DRY RUN MODE - Would post to r/{Subreddit}", subreddit);
                Log.Information("Title: {Title}", title);
                Log.Information("Body preview (first 200 chars): {BodyPreview}",
                    body.Length > 200 ? body.Substring(0, 200) + "..." : body);
                Log.Information("Body length: {Length} characters", body.Length);
                Log.Information("DRY RUN COMPLETE - No actual post was made");
                return true;
            }

            // Attempt to post with retry policy
            string postId = "";
            string postUrl = "";
            int attemptCount = 0;

            try
            {
                await _retryPolicy.ExecuteAsync(async () =>
                {
                    attemptCount++;
                    _historyManager.RecordPostAttempt(date, title, attemptCount - 1);

                    try
                    {
                        (postId, postUrl) = await _redditClient.SubmitPostAsync(subreddit, title, body);
                    }
                    catch (Exception ex)
                    {
                        retryHistory.Add((DateTime.Now, ex.Message));
                        throw;
                    }
                });
            }
            catch (Exception ex)
            {
                // All retries exhausted
                stopwatch.Stop();
                Log.Error(ex, "Failed to post after {Attempts} attempts (1 initial + {Retries} retries)",
                    attemptCount, _settings.Retry.MaxRetries);

                _historyManager.RecordFailure(date, title, ex.Message, attemptCount - 1, stopwatch.ElapsedMilliseconds);

                // Send failure notification email
                try
                {
                    var dailyPath = Environment.ExpandEnvironmentVariables(_settings.Posting.DailyFolderPath);
                    var filePath = Path.Combine(dailyPath, $"{date}.txt");

                    await _emailService.SendFailureNotificationAsync(
                        date,
                        title,
                        retryHistory,
                        filePath,
                        body.Length > 200 ? body.Substring(0, 200) : body);
                }
                catch (Exception emailEx)
                {
                    Log.Error(emailEx, "Failed to send failure notification email");
                }

                return false;
            }

            // Verify the post was created successfully
            Log.Information("Verifying post was created successfully");
            bool verified = await _redditClient.VerifyPostAsync(postId);

            if (!verified)
            {
                Log.Warning("Post verification failed, but post was submitted. Marking as success anyway.");
            }

            stopwatch.Stop();

            // Record success in history
            _historyManager.RecordSuccess(date, postId, postUrl, title, stopwatch.ElapsedMilliseconds, attemptCount - 1);

            Log.Information("Successfully posted daily content for {Date}. Post URL: {PostUrl}, Execution time: {ElapsedMs}ms",
                date, postUrl, stopwatch.ElapsedMilliseconds);

            return true;
        }
        catch (FileNotFoundException ex)
        {
            Log.Error(ex, "Daily content file not found for {Date}", date);
            stopwatch.Stop();
            _historyManager.RecordFailure(date, "", ex.Message, 0, stopwatch.ElapsedMilliseconds);

            try
            {
                await _emailService.SendFailureNotificationAsync(
                    date,
                    "Unknown",
                    new List<(DateTime, string)> { (DateTime.Now, ex.Message) });
            }
            catch (Exception emailEx)
            {
                Log.Error(emailEx, "Failed to send failure notification email");
            }

            return false;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Unexpected error during post process");
            stopwatch.Stop();
            _historyManager.RecordFailure(date, "", ex.Message, 0, stopwatch.ElapsedMilliseconds);
            return false;
        }
    }

    public async Task<bool> ValidateConfigurationAsync()
    {
        Log.Information("Validating configuration and connectivity");

        try
        {
            // Test Reddit API connection
            Log.Information("Testing Reddit API authentication");
            var token = await _redditClient.GetAccessTokenAsync();
            Log.Information("✓ Reddit API authentication successful");

            // Validate daily folder exists
            var dailyPath = Environment.ExpandEnvironmentVariables(_settings.Posting.DailyFolderPath);
            if (!Directory.Exists(dailyPath))
            {
                Log.Error("✗ Daily folder not found: {DailyPath}", dailyPath);
                return false;
            }
            Log.Information("✓ Daily folder exists: {DailyPath}", dailyPath);

            // Check today's file exists
            var easternZone = TimeZoneInfo.FindSystemTimeZoneById(_settings.Posting.TimeZone);
            var easternNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, easternZone);
            _contentProvider.ValidateDailyFile(easternNow);
            Log.Information("✓ Today's daily file exists and is valid");

            Log.Information("✓ All configuration validation checks passed");
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Configuration validation failed");
            return false;
        }
    }
}
