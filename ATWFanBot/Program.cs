using System.CommandLine;
using Microsoft.Extensions.Configuration;
using ATWFanBot.Configuration;
using ATWFanBot.Services;
using Serilog;

namespace ATWFanBot;

class Program
{
    static async Task<int> Main(string[] args)
    {
        // Build configuration
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
            .AddEnvironmentVariables()
            .AddUserSecrets<Program>(optional: true)
            .Build();

        var settings = configuration.Get<AppSettings>();

        // Load secrets from environment variables
        var secrets = Secrets.LoadFromEnvironment();

        // Set up Serilog logging
        var logDirectory = Environment.ExpandEnvironmentVariables(settings.Logging.LogDirectory);
        Directory.CreateDirectory(logDirectory);

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.Console(
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
            .WriteTo.File(
                path: Path.Combine(logDirectory, "atwfanbot-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: settings.Logging.RetainDays,
                outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} {Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        try
        {
            Log.Information("=== ATWFanBot v1.0 Starting ===");
            Log.Information("Log directory: {LogDirectory}", logDirectory);

            // Define CLI options
            var rootCommand = new RootCommand("ATWFanBot - Automated daily Reddit posting bot for r/AdamTheWoo");

            var dryRunOption = new Option<bool>(
                aliases: new[] { "--dry-run", "-d" },
                description: "Perform a dry run without actually posting to Reddit");

            var subredditOption = new Option<string?>(
                aliases: new[] { "--subreddit", "-s" },
                description: "Override the target subreddit (useful for testing)");

            var validateOption = new Option<bool>(
                aliases: new[] { "--validate", "-v" },
                description: "Validate configuration and exit");

            var validateAllOption = new Option<bool>(
                aliases: new[] { "--validate-all" },
                description: "Validate all daily content files and report any issues");

            var testEmailOption = new Option<bool>(
                aliases: new[] { "--test-email" },
                description: "Send a test email to verify email configuration");

            rootCommand.AddOption(dryRunOption);
            rootCommand.AddOption(subredditOption);
            rootCommand.AddOption(validateOption);
            rootCommand.AddOption(validateAllOption);
            rootCommand.AddOption(testEmailOption);

            rootCommand.SetHandler(async (dryRun, subreddit, validate, validateAll, testEmail) =>
            {
                await ExecuteAsync(settings, secrets, dryRun, subreddit, validate, validateAll, testEmail);
            }, dryRunOption, subredditOption, validateOption, validateAllOption, testEmailOption);

            return await rootCommand.InvokeAsync(args);
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Application terminated unexpectedly");
            return 1;
        }
        finally
        {
            Log.Information("=== ATWFanBot Shutdown ===");
            Log.CloseAndFlush();
        }
    }

    static async Task ExecuteAsync(
        AppSettings settings,
        Secrets secrets,
        bool dryRun,
        string? subreddit,
        bool validate,
        bool validateAll,
        bool testEmail)
    {
        try
        {
            // Validate secrets (unless just validating files)
            if (!validateAll)
            {
                secrets.ValidateOrThrow();
            }

            // Initialize services
            var redditClient = new RedditApiClient(settings.Reddit, secrets);
            var contentProvider = new DailyFileContentProvider(settings);
            var historyManager = new PostHistoryManager(settings.Posting);
            var emailService = new EmailNotificationService(settings.Email, secrets);
            var postingService = new PostingService(
                settings,
                secrets,
                redditClient,
                contentProvider,
                historyManager,
                emailService);

            // Handle --validate-all
            if (validateAll)
            {
                Log.Information("Validating all daily content files...");
                var errors = contentProvider.ValidateAllDailyFiles();

                if (errors.Count == 0)
                {
                    Log.Information("✓ All daily content files validated successfully!");
                    Console.WriteLine("\n✓ SUCCESS: All daily content files are present and valid.");
                }
                else
                {
                    Log.Warning("Found {ErrorCount} issues with daily files:", errors.Count);
                    foreach (var error in errors)
                    {
                        Log.Warning("  - {Error}", error);
                        Console.WriteLine($"  ✗ {error}");
                    }
                    Console.WriteLine($"\nFound {errors.Count} issues. Please fix these before running the bot.");
                    Environment.Exit(1);
                }
                return;
            }

            // Handle --test-email
            if (testEmail)
            {
                Log.Information("Sending test email...");
                await emailService.SendTestEmailAsync();
                Console.WriteLine("\n✓ Test email sent successfully. Check your inbox.");
                return;
            }

            // Handle --validate
            if (validate)
            {
                var isValid = await postingService.ValidateConfigurationAsync();
                if (isValid)
                {
                    Console.WriteLine("\n✓ SUCCESS: All configuration checks passed!");
                    Console.WriteLine("  ✓ Reddit API authentication successful");
                    Console.WriteLine("  ✓ Daily content folder exists");
                    Console.WriteLine("  ✓ Today's daily content file is valid");
                    Console.WriteLine("\nReady to post!");
                }
                else
                {
                    Console.WriteLine("\n✗ FAILED: Configuration validation failed. Check logs for details.");
                    Environment.Exit(1);
                }
                return;
            }

            // Normal posting mode
            if (dryRun)
            {
                Log.Information("Running in DRY RUN mode");
            }

            if (subreddit != null)
            {
                Log.Information("Overriding subreddit to: r/{Subreddit}", subreddit);
            }

            var success = await postingService.PostDailyContentAsync(subreddit, dryRun);

            if (success)
            {
                Console.WriteLine("\n✓ SUCCESS: Daily post completed successfully!");
                Environment.Exit(0);
            }
            else
            {
                Console.WriteLine("\n✗ FAILED: Post failed after all retries. Check logs for details.");
                Environment.Exit(1);
            }
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Missing required environment variables"))
        {
            Log.Error("Missing configuration: {Message}", ex.Message);
            Console.WriteLine("\n✗ ERROR: Missing required environment variables");
            Console.WriteLine("\nPlease set the following environment variables:");
            Console.WriteLine("  - REDDIT_CLIENT_ID");
            Console.WriteLine("  - REDDIT_CLIENT_SECRET");
            Console.WriteLine("  - REDDIT_USERNAME");
            Console.WriteLine("  - REDDIT_PASSWORD");
            Console.WriteLine("  - SMTP_USERNAME");
            Console.WriteLine("  - SMTP_PASSWORD");
            Console.WriteLine("  - NOTIFICATION_EMAIL");
            Console.WriteLine("\nSee README.md for detailed setup instructions.");
            Environment.Exit(1);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Fatal error during execution");
            Console.WriteLine($"\n✗ ERROR: {ex.Message}");
            Console.WriteLine("\nCheck logs for detailed error information.");
            Environment.Exit(1);
        }
    }
}
