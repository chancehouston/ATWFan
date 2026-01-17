# Implementation Plan — Daily Reddit Poster (Console App)

Objective
- Build a .NET 8 console application that runs once daily (scheduled externally) to launch a headless browser, log into Reddit, create a post in a specified subreddit, then exit.

High-level design
- The console app performs a single run-and-exit job. Scheduling is done with the host OS (Windows Task Scheduler or systemd timer) per Microsoft guidance: let the OS schedule recurring runs for simple background tasks.
- Use a browser automation framework to perform interactions with Reddit through a real browser session. The project will use `Microsoft.Playwright` for .NET (recommended for reliability and cross-platform headless automation).
- Store secrets (Reddit username/password or OAuth tokens) in environment variables or a secure secret store. Do not hard-code credentials.

Security and policy notes
- Prefer using Reddit API with OAuth for programmatic posting when possible (more stable and compliant). For this iteration the app will use browser automation; follow Reddit terms of service and avoid abusive automation.
- Keep credentials out of source control. Use environment variables, user secret stores, or OS credential managers.

Libraries and tools
- .NET 8 SDK
- `Microsoft.Playwright` (Playwright for .NET) — recommended for browser automation. See Playwright docs for installation and best practices.
- `Microsoft.Extensions.Configuration` to manage configuration and environment variables.
- Logging: `Microsoft.Extensions.Logging`.

Project structure (files to add/change)
- `Program.cs` — CLI entry; parse arguments, load configuration, run the automation job.
- `src/PostJob.cs` — class that encapsulates the automation flow (launch browser, login, create post, cleanup).
- `src/RedditClient.cs` — helper to manage login/session cookies and page interactions (selectors abstracted here).
- `src/DailyPostProvider.cs` — reads the daily post file from the `Daily` folder and exposes `Title` and `Body`.
- `src/SecretsConfiguration.cs` — config binding for credentials, subreddit, post templates.
- `IMPLEMENTATION_PLAN.md` — this plan.

Post source and format
- The app will read post content from a local `Daily` folder located in the application's working directory (or a configurable path).
- Files are named by date in `MM-DD.txt` format (e.g., `01-15.txt`).
- Each file's first line is the post title; the remaining lines form the post body (plain text).
- If no file exists for the current date, the app should log an appropriate error and exit without attempting to post.

Implementation steps
1. Initialize
   - Ensure project targets .NET 8.
   - Add `Microsoft.Playwright` NuGet package and run `playwright install` during development.
2. Configuration
   - Use environment variables for secrets and settings. Defaults for this project:
     - `REDDIT_USERNAME` — Reddit username
     - `REDDIT_PASSWORD` — Reddit password
     - `SUBREDDIT` — subreddit to post to (default: `AdamTheWoo`)
     - `DAILY_FOLDER` — optional path to `Daily` folder (default: `./Daily`)
     - `POST_TITLE` / `POST_BODY` — optional overrides. If provided, they take precedence over the daily file.
   - Optionally support `appsettings.json` for non-secret defaults; environment variables override appsettings.
3. Implement the automation flow (`PostJob`)
   - Instantiate `DailyPostProvider` which:
     - Computes today's filename using the system date and `MM-dd` format.
     - Looks for `MM-DD.txt` in the configured `Daily` folder.
     - Reads the first line as `Title` and the remaining content as `Body`.
     - Exposes a method to validate presence and content; returns an error if missing.
   - If `POST_TITLE` or `POST_BODY` environment variables are set, use them instead of the daily file values.
   - Launch Playwright and create a `IBrowser` instance in headless mode by default; provide a `--debug` or configuration option to run headed for troubleshooting.
   - Create a new `IPage` and navigate to Reddit login page.
   - Perform login using selectors and await navigation / success indicators.
   - Navigate to `https://www.reddit.com/r/AdamTheWoo/` (or configured subreddit) and click `Create Post`.
   - Fill title and body (plain text only), select flair if applicable, and submit.
   - Wait for confirmation (post permalink present or success toast).
   - Close page and browser.
   - Optionally persist session cookies to disk to reduce login frequency; ensure cookie file is secured.
4. Error handling and logging
   - If the daily file is missing or malformed, log a clear message and exit without posting.
   - Add retries for transient failures (network, dynamic page changes).
   - Log detailed error info and non-sensitive diagnostics. Do not log credentials.
5. Tests and dry runs
   - Run manual dry runs with a test account or private subreddit.
   - Include a mode that runs with visible (headed) browser for debugging.
6. Scheduling
   - Windows: create a Task Scheduler task to run the console app daily at chosen time. Configure "Run only when user is logged on" vs "Run whether user is logged on" and provide credentials.
   - Linux: create a systemd timer or cron job to run daily.
   - Use environment variables or a config file to set time zone-sensitive behavior.
7. Deployment
   - Publish a self-contained single-file app (`dotnet publish -r win-x64 --self-contained`) if needed for portability.
   - Place executable on host machine, configure scheduled task specifying working directory and environment variables.

Selectors and resilience
- Avoid brittle selectors (prefer data attributes if present). Use retries and wait-for conditions like `WaitForSelectorAsync` and `WaitForNavigationAsync`.
- Add a short configurable delay between navigation and clicks to mimic human pacing.

Cookie/session handling
- Optionally persist cookies to reduce login frequency. Encrypt cookie file at rest or use OS-provided secret stores.

Logging, monitoring, and notifications
- Emit structured logs. For critical failures, optionally send a notification (email or webhook).

Deliverables (first iteration)
- `Program.cs` that instantiates `PostJob` and runs it.
- `PostJob.cs` implementing automation flow using Playwright.
- `DailyPostProvider.cs` to read daily `MM-DD.txt` files and supply title/body.
- Config and README with scheduling instructions (Windows Task Scheduler + systemd examples), security guidance for storing secrets, and how to run locally with visible browser for debugging.

Open questions (answered)
- Automation method: browser automation (Playwright) — confirmed.
- Subreddit: `r/AdamTheWoo` — confirmed.
- Credentials: provided via environment variables (`REDDIT_USERNAME`, `REDDIT_PASSWORD`) — confirmed.
- Post type: plain text posts only (no attachments) — confirmed.

References / further reading
- Playwright for .NET docs (recommended): https://playwright.dev/dotnet
- Microsoft guidance: use the OS scheduler for recurring console apps; consider `Microsoft.Extensions.Hosting` for long-running services and worker templates.

Next steps
- I will implement the minimal first iteration: a `PostJob` using Playwright, `DailyPostProvider` to read `Daily/MM-DD.txt`, configuration via environment variables (with `SUBREDDIT` defaulting to `AdamTheWoo`), and README instructions for scheduling. Then run a local debug flow.

