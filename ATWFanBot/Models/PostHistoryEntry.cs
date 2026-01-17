namespace ATWFanBot.Models;

public class PostHistoryEntry
{
    public string Date { get; set; } = string.Empty;
    public string? PostId { get; set; }
    public string? PostUrl { get; set; }
    public string? Title { get; set; }
    public DateTime Timestamp { get; set; }
    public string Status { get; set; } = "pending";
    public int Retries { get; set; }
    public long ExecutionTimeMs { get; set; }
    public string? ErrorMessage { get; set; }
}

public class PostHistory
{
    public List<PostHistoryEntry> Posts { get; set; } = new();
    public PostStatistics Statistics { get; set; } = new();
}

public class PostStatistics
{
    public int TotalPosts { get; set; }
    public double SuccessRate { get; set; }
    public long AverageExecutionTimeMs { get; set; }
}
