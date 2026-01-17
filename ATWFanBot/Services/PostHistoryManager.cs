using System.Text.Json;
using ATWFanBot.Configuration;
using ATWFanBot.Models;
using Serilog;

namespace ATWFanBot.Services;

public class PostHistoryManager
{
    private readonly string _historyFilePath;
    private PostHistory _history;
    private readonly object _lock = new();

    public PostHistoryManager(PostingSettings settings)
    {
        _historyFilePath = Environment.ExpandEnvironmentVariables(settings.HistoryFilePath);
        EnsureDirectoryExists();
        LoadHistory();
    }

    private void EnsureDirectoryExists()
    {
        var directory = Path.GetDirectoryName(_historyFilePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
            Log.Information("Created history directory: {Directory}", directory);
        }
    }

    private void LoadHistory()
    {
        lock (_lock)
        {
            if (!File.Exists(_historyFilePath))
            {
                Log.Information("Post history file not found, creating new history: {FilePath}", _historyFilePath);
                _history = new PostHistory();
                SaveHistory();
                return;
            }

            try
            {
                var json = File.ReadAllText(_historyFilePath);
                _history = JsonSerializer.Deserialize<PostHistory>(json) ?? new PostHistory();
                Log.Information("Loaded post history with {Count} entries", _history.Posts.Count);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to load post history, creating new history");
                _history = new PostHistory();
            }
        }
    }

    private void SaveHistory()
    {
        lock (_lock)
        {
            try
            {
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true
                };
                var json = JsonSerializer.Serialize(_history, options);
                File.WriteAllText(_historyFilePath, json);
                Log.Debug("Saved post history to {FilePath}", _historyFilePath);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to save post history");
                throw;
            }
        }
    }

    public bool HasSuccessfulPostForDate(string date)
    {
        lock (_lock)
        {
            var entry = _history.Posts.FirstOrDefault(p => p.Date == date);
            var hasPost = entry != null && entry.Status == "success";

            if (hasPost)
            {
                Log.Information("Found existing successful post for date {Date}: {PostUrl}", date, entry!.PostUrl);
            }

            return hasPost;
        }
    }

    public PostHistoryEntry? GetEntryForDate(string date)
    {
        lock (_lock)
        {
            return _history.Posts.FirstOrDefault(p => p.Date == date);
        }
    }

    public void RecordPostAttempt(string date, string title, int retryCount = 0)
    {
        lock (_lock)
        {
            var entry = _history.Posts.FirstOrDefault(p => p.Date == date);
            if (entry == null)
            {
                entry = new PostHistoryEntry
                {
                    Date = date,
                    Title = title,
                    Timestamp = DateTime.UtcNow,
                    Status = "pending"
                };
                _history.Posts.Add(entry);
            }

            entry.Retries = retryCount;
            SaveHistory();
        }
    }

    public void RecordSuccess(string date, string postId, string postUrl, string title, long executionTimeMs, int retryCount)
    {
        lock (_lock)
        {
            var entry = _history.Posts.FirstOrDefault(p => p.Date == date);
            if (entry == null)
            {
                entry = new PostHistoryEntry { Date = date };
                _history.Posts.Add(entry);
            }

            entry.PostId = postId;
            entry.PostUrl = postUrl;
            entry.Title = title;
            entry.Timestamp = DateTime.UtcNow;
            entry.Status = "success";
            entry.Retries = retryCount;
            entry.ExecutionTimeMs = executionTimeMs;
            entry.ErrorMessage = null;

            UpdateStatistics();
            SaveHistory();

            Log.Information("Recorded successful post for {Date} in history", date);
        }
    }

    public void RecordFailure(string date, string title, string errorMessage, int retryCount, long executionTimeMs)
    {
        lock (_lock)
        {
            var entry = _history.Posts.FirstOrDefault(p => p.Date == date);
            if (entry == null)
            {
                entry = new PostHistoryEntry { Date = date };
                _history.Posts.Add(entry);
            }

            entry.Title = title;
            entry.Timestamp = DateTime.UtcNow;
            entry.Status = "failed";
            entry.Retries = retryCount;
            entry.ExecutionTimeMs = executionTimeMs;
            entry.ErrorMessage = errorMessage;

            UpdateStatistics();
            SaveHistory();

            Log.Warning("Recorded failed post for {Date} in history", date);
        }
    }

    private void UpdateStatistics()
    {
        var successfulPosts = _history.Posts.Where(p => p.Status == "success").ToList();
        var totalPosts = _history.Posts.Count(p => p.Status == "success" || p.Status == "failed");

        _history.Statistics.TotalPosts = successfulPosts.Count;
        _history.Statistics.SuccessRate = totalPosts > 0
            ? (double)successfulPosts.Count / totalPosts
            : 0;
        _history.Statistics.AverageExecutionTimeMs = successfulPosts.Any()
            ? (long)successfulPosts.Average(p => p.ExecutionTimeMs)
            : 0;
    }

    public PostHistory GetHistory()
    {
        lock (_lock)
        {
            return _history;
        }
    }
}
