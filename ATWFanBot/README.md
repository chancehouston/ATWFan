# ATWFanBot

Automated daily Reddit posting bot for r/AdamTheWoo. Posts "On This Day" content from historical videos at 9:00 AM Eastern Time daily.

## Features

- ✅ **Reddit API Integration**: Uses official Reddit OAuth API (not browser automation)
- ✅ **Idempotency**: Prevents duplicate posts using PostHistory.json tracking
- ✅ **Retry Logic**: 3 retries with 10-minute delays on failures
- ✅ **Email Notifications**: Sends email alerts when posts fail after all retries
- ✅ **Timezone Aware**: Posts at 9:00 AM US Eastern Time
- ✅ **Dry Run Mode**: Test without actually posting
- ✅ **Comprehensive Logging**: Structured logging with Serilog (console + rolling files)
- ✅ **Validation Tools**: Validate configuration and daily content files
- ✅ **Post Verification**: Confirms posts were created successfully

## Prerequisites

- .NET 8.0 Runtime or SDK
- Reddit account with API access
- Gmail/Outlook account for email notifications
- Windows PC (for production deployment)

## Quick Start

### 1. Register Reddit Application

1. Go to https://www.reddit.com/prefs/apps
2. Click "create another app..." at the bottom
3. Fill in:
   - **Name**: ATWFanBot (or your choice)
   - **App type**: Select **"script"**
   - **Description**: Daily automated posts for r/AdamTheWoo
   - **Redirect URI**: `http://localhost:8080` (required but not used)
4. Click "create app"
5. Save these values:
   - **Client ID**: The string under the app name (~14 characters)
   - **Client Secret**: The longer string labeled "secret" (~27 characters)

### 2. Set Environment Variables

Set the following environment variables on your system:

**Reddit Credentials:**
```bash
REDDIT_CLIENT_ID=your_client_id_here
REDDIT_CLIENT_SECRET=your_client_secret_here
REDDIT_USERNAME=your_reddit_username
REDDIT_PASSWORD=your_reddit_password
```

**Email Configuration (Gmail example):**
```bash
SMTP_USERNAME=your-email@gmail.com
SMTP_PASSWORD=your_gmail_app_password
NOTIFICATION_EMAIL=your-email@gmail.com
```

**For Gmail:**
- You must enable 2FA on your Gmail account
- Generate an App Password at: https://myaccount.google.com/apppasswords
- Use the 16-character app password (NOT your regular Gmail password)

**Windows (PowerShell - User variables, persistent):**
```powershell
[System.Environment]::SetEnvironmentVariable('REDDIT_CLIENT_ID', 'your_value', 'User')
[System.Environment]::SetEnvironmentVariable('REDDIT_CLIENT_SECRET', 'your_value', 'User')
[System.Environment]::SetEnvironmentVariable('REDDIT_USERNAME', 'your_value', 'User')
[System.Environment]::SetEnvironmentVariable('REDDIT_PASSWORD', 'your_value', 'User')
[System.Environment]::SetEnvironmentVariable('SMTP_USERNAME', 'your_value', 'User')
[System.Environment]::SetEnvironmentVariable('SMTP_PASSWORD', 'your_value', 'User')
[System.Environment]::SetEnvironmentVariable('NOTIFICATION_EMAIL', 'your_value', 'User')
```

### 3. Configure appsettings.json

Edit `appsettings.json` to customize settings:

- **Reddit.UserAgent**: Replace `REPLACE_WITH_YOUR_USERNAME` with your Reddit username
- **Reddit.Subreddit**: Change if testing on a different subreddit
- **Posting.DailyFolderPath**: Path to your Daily folder (default: `../Daily`)
- **Email.SmtpHost/SmtpPort**: Change if using Outlook or other SMTP provider

### 4. Build the Application

```bash
cd ATWFanBot
dotnet build
```

### 5. Validate Configuration

Test that everything is set up correctly:

```bash
dotnet run -- --validate
```

This will:
- ✓ Test Reddit API authentication
- ✓ Verify Daily folder exists
- ✓ Check today's daily content file

### 6. Test Email Notifications

```bash
dotnet run -- --test-email
```

### 7. Dry Run

Test the full posting flow without actually posting:

```bash
dotnet run -- --dry-run
```

### 8. Test Post to Test Subreddit

Before posting to r/AdamTheWoo, test on your own subreddit:

```bash
dotnet run -- --subreddit YourTestSubreddit
```

### 9. Manual Post

Once validated, run manually to post:

```bash
dotnet run
```

## Command-Line Options

```
ATWFanBot - Automated daily Reddit posting bot for r/AdamTheWoo

Options:
  --dry-run, -d           Perform a dry run without actually posting to Reddit
  --subreddit, -s <name>  Override the target subreddit (useful for testing)
  --validate, -v          Validate configuration and exit
  --validate-all          Validate all daily content files (366 files)
  --test-email            Send a test email to verify email configuration
  --help                  Display help
  --version               Display version information
```

## Scheduling (Windows)

### Option 1: Windows Task Scheduler (Recommended)

1. Open Task Scheduler
2. Create Basic Task:
   - **Name**: ATWFanBot Daily Post
   - **Trigger**: Daily at 9:00 AM
   - **Action**: Start a program
     - **Program**: `C:\path\to\dotnet.exe`
     - **Arguments**: `run --project C:\path\to\ATWFanBot`
     - Or use the compiled exe: `C:\path\to\ATWFanBot.exe`
   - **Settings**:
     - ✓ Run whether user is logged on or not
     - ✓ Wake the computer to run this task
     - Stop task if runs longer than: 1 hour

### Option 2: PowerShell Script

Create `run-atwfanbot.ps1`:
```powershell
cd C:\path\to\ATWFanBot
dotnet run
```

Schedule this script in Task Scheduler.

## File Structure

```
ATWFanBot/
├── appsettings.json                 # Configuration
├── ATWFanBot.csproj                 # Project file
├── Program.cs                       # Entry point
├── Configuration/
│   ├── AppSettings.cs               # Settings models
│   └── Secrets.cs                   # Environment variable loader
├── Models/
│   ├── PostHistoryEntry.cs          # Post history models
│   └── RedditModels.cs              # Reddit API models
└── Services/
    ├── RedditApiClient.cs           # Reddit API client
    ├── DailyFileContentProvider.cs  # Daily file reader
    ├── PostHistoryManager.cs        # Post tracking
    ├── EmailNotificationService.cs  # Email notifications
    └── PostingService.cs            # Main orchestration
```

## Data Files

**Post History** (Auto-created):
- Location: `%LOCALAPPDATA%\ATWFanBot\PostHistory.json`
- Tracks all posts to prevent duplicates
- Includes statistics (success rate, average execution time)

**Logs** (Auto-created):
- Location: `%LOCALAPPDATA%\ATWFanBot\Logs\`
- Rolling log files (one per day)
- Retained for 30 days (configurable)

**Daily Content Files** (You provide):
- Location: `../Daily/` (relative to executable)
- Format: `MM-dd.txt` (e.g., `01-17.txt`, `12-25.txt`)
- Required: 366 files (including Feb 29 for leap years)

## Daily Content File Format

Each file should contain the post body in markdown format:

```
Videos for this date:

[Video Title 1](https://youtube.com/watch?v=VIDEO_ID)
Description...

[Video Title 2](https://youtube.com/watch?v=VIDEO_ID)
Description...
```

**Character Limit**: Reddit allows up to 40,000 characters for self posts.

## Validation

Validate all 366 daily files:

```bash
dotnet run -- --validate-all
```

This reports:
- Missing files (e.g., `Missing file: 02-29.txt`)
- Empty files
- Files exceeding character limit

## Troubleshooting

### "Missing required environment variables"

**Solution**: Set all 7 required environment variables (see step 2 above). Restart your terminal/PowerShell after setting them.

### "Failed to obtain Reddit access token"

**Possible causes:**
- Incorrect REDDIT_CLIENT_ID or REDDIT_CLIENT_SECRET
- Incorrect REDDIT_USERNAME or REDDIT_PASSWORD
- Reddit account banned or suspended

**Solution**:
1. Verify credentials are correct
2. Test login manually on reddit.com
3. Check app is "script" type at https://www.reddit.com/prefs/apps

### "Failed to send email"

**Possible causes:**
- Incorrect SMTP credentials
- Gmail: Using regular password instead of App Password
- Firewall blocking SMTP port 587

**Solution**:
1. For Gmail, generate App Password: https://myaccount.google.com/apppasswords
2. Test with `dotnet run -- --test-email`
3. Check firewall/antivirus settings

### "Daily content file not found"

**Possible causes:**
- Missing MM-dd.txt file for today's date
- Incorrect DailyFolderPath in appsettings.json

**Solution**:
1. Check file exists: `../Daily/01-17.txt` (for Jan 17)
2. Verify path in appsettings.json points to Daily folder
3. Use `--validate-all` to check all files

### "Post for date already exists in history"

This is **normal** if the bot already posted today. The idempotency check prevents duplicate posts.

**To force re-post** (testing only):
1. Open `%LOCALAPPDATA%\ATWFanBot\PostHistory.json`
2. Remove the entry for today's date
3. Save and run again

### Rate Limiting (429 errors)

**Cause**: Reddit rate limits API requests

**Solution**: This should not happen with daily posting (bot makes ~3 requests per day). If it does:
1. Verify UserAgent is set correctly in appsettings.json
2. Wait 10 minutes and try again
3. Check you're not running multiple instances

## Monitoring

### Health Status File

Location: `%LOCALAPPDATA%\ATWFanBot\HealthStatus.json` (future feature)

### Email Notifications

You'll receive an email if:
- ✉ Post fails after all 3 retries (includes error details and recovery steps)

### Logs

Check logs for detailed execution information:
```
%LOCALAPPDATA%\ATWFanBot\Logs\atwfanbot-YYYYMMDD.log
```

## Security Best Practices

1. ✅ **Never commit secrets to git**
2. ✅ Use environment variables for all credentials
3. ✅ For Gmail, use App Passwords (never your real password)
4. ✅ Set User environment variables (not System) on Windows
5. ✅ Restrict file permissions on PostHistory.json if it contains sensitive data
6. ✅ Use a dedicated Reddit account (not your main account)
7. ✅ Rotate Reddit credentials every 90 days

## Architecture

Built following the recommendations from PLAN_REVIEW_AND_RECOMMENDATIONS.md:

- **Reddit API** (not browser automation) for reliability
- **Polly** for retry logic with 10-minute delays
- **Serilog** for structured logging
- **MailKit** for email notifications
- **System.CommandLine** for CLI parsing
- **Idempotency** via PostHistory.json tracking
- **Timezone awareness** for Eastern Time posting

## Support

For issues, check:
1. Application logs in `%LOCALAPPDATA%\ATWFanBot\Logs\`
2. PostHistory.json for post tracking
3. Run `--validate` to check configuration
4. Run `--validate-all` to check all daily files

## License

Private project for r/AdamTheWoo community.

## Version History

**v1.0** (2026-01-17)
- Initial release
- Reddit API integration
- Email notifications
- Idempotency tracking
- Retry logic with Polly
- Comprehensive logging
- CLI with validation tools
