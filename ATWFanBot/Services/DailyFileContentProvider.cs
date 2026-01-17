using ATWFanBot.Configuration;
using Serilog;

namespace ATWFanBot.Services;

public class DailyFileContentProvider
{
    private readonly PostingSettings _settings;

    public DailyFileContentProvider(PostingSettings settings)
    {
        _settings = settings;
    }

    public (string title, string body) GetDailyContent(DateTime date, string? subreddit = null)
    {
        var dateStr = date.ToString("MM-dd");
        var monthName = date.ToString("MMMM");
        var dayNumber = date.Day;
        var daySuffix = GetDaySuffix(dayNumber);

        // Expand environment variables in path
        var dailyPath = Environment.ExpandEnvironmentVariables(_settings.DailyFolderPath);

        var fileName = $"{dateStr}.txt";
        var filePath = Path.Combine(dailyPath, fileName);

        Log.Information("Loading daily content from: {FilePath}", filePath);

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException(
                $"Daily content file not found: {filePath}. " +
                $"Please create a file named '{fileName}' in the Daily folder.");
        }

        var body = File.ReadAllText(filePath);

        if (string.IsNullOrWhiteSpace(body))
        {
            throw new InvalidOperationException(
                $"Daily content file is empty: {filePath}");
        }

        var title = string.Format(_settings.PostTitleTemplate,
            $"{monthName} {dayNumber}{daySuffix}");

        Log.Information("Loaded daily content. Title: {Title}, Body length: {BodyLength} characters",
            title, body.Length);

        return (title, body);
    }

    private static string GetDaySuffix(int day)
    {
        return day switch
        {
            1 or 21 or 31 => "st",
            2 or 22 => "nd",
            3 or 23 => "rd",
            _ => "th"
        };
    }

    public void ValidateDailyFile(DateTime date)
    {
        var dateStr = date.ToString("MM-dd");
        var dailyPath = Environment.ExpandEnvironmentVariables(_settings.DailyFolderPath);
        var fileName = $"{dateStr}.txt";
        var filePath = Path.Combine(dailyPath, fileName);

        if (!Directory.Exists(dailyPath))
        {
            throw new DirectoryNotFoundException(
                $"Daily folder not found: {dailyPath}");
        }

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException(
                $"Daily content file not found: {filePath}");
        }

        var content = File.ReadAllText(filePath);
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new InvalidOperationException(
                $"Daily content file is empty: {filePath}");
        }

        // Check character limit (Reddit limit is 40,000 characters)
        if (content.Length > 40000)
        {
            throw new InvalidOperationException(
                $"Daily content exceeds Reddit's 40,000 character limit: {content.Length} characters");
        }

        Log.Information("Daily file validation passed: {FilePath}", filePath);
    }

    public List<string> ValidateAllDailyFiles()
    {
        var dailyPath = Environment.ExpandEnvironmentVariables(_settings.DailyFolderPath);
        var errors = new List<string>();

        if (!Directory.Exists(dailyPath))
        {
            errors.Add($"Daily folder not found: {dailyPath}");
            return errors;
        }

        // Check for all 366 possible dates (including Feb 29)
        for (int month = 1; month <= 12; month++)
        {
            int daysInMonth = DateTime.DaysInMonth(2024, month); // Using 2024 (leap year)
            for (int day = 1; day <= daysInMonth; day++)
            {
                var dateStr = $"{month:D2}-{day:D2}";
                var fileName = $"{dateStr}.txt";
                var filePath = Path.Combine(dailyPath, fileName);

                if (!File.Exists(filePath))
                {
                    errors.Add($"Missing file: {fileName}");
                }
                else
                {
                    var content = File.ReadAllText(filePath);
                    if (string.IsNullOrWhiteSpace(content))
                    {
                        errors.Add($"Empty file: {fileName}");
                    }
                    else if (content.Length > 40000)
                    {
                        errors.Add($"File exceeds character limit: {fileName} ({content.Length} chars)");
                    }
                }
            }
        }

        return errors;
    }
}
