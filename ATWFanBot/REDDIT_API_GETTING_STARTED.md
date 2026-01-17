# Reddit API Quick Start Guide for Script Apps

Comprehensive guide for using Reddit's API with script-type applications using the OAuth2 password grant flow.

## Table of Contents
1. [Prerequisites & Limitations](#prerequisites--limitations)
2. [Register a Script Application](#1-register-a-script-application)
3. [Understanding Authentication](#2-understanding-script-app-authentication)
4. [Obtain an Access Token](#3-obtain-an-access-token)
5. [Use the Token for API Calls](#4-use-the-token-for-api-calls)
6. [Rate Limits](#5-rate-limits)
7. [Common Errors & Troubleshooting](#6-common-errors--troubleshooting)
8. [Best Practices](#7-best-practices)
9. [.NET Implementation](#8-net-implementation)

---

## Prerequisites & Limitations

⚠️ **Critical limitations for script-type apps:**

- **Developer-only access**: Script apps ONLY work with accounts registered as "developers" of the app
- **No 2FA support**: The Reddit account **must not** have two-factor authentication enabled
  - If 2FA is enabled, Reddit will reject API authentication with username/password
  - You must disable 2FA or use a different authentication method
- **Password required**: Script apps require knowing the user's actual Reddit password
- **Personal use**: Best suited for personal automation or bots running under your own account
- **Single account**: Designed for the developer's own account, not multiple users

✅ **When to use script apps:**
- Personal automation tools
- Bots running under your account
- Internal tools with a single Reddit account
- Development and testing

❌ **When NOT to use script apps:**
- Public applications with multiple users
- Apps requiring 2FA-protected accounts
- Applications needing to act on behalf of many users

---

## 1. Register a Script Application

### Step-by-Step Registration

1. Navigate to **https://www.reddit.com/prefs/apps**
2. Scroll down and click **"create another app..."**
3. Fill in the application form:

   | Field | Value |
   |-------|-------|
   | **name** | Your bot name (e.g., "ATWFanBot") |
   | **App type** | ⚠️ **"script"** (CRITICAL - must select this option) |
   | **description** | Brief description of your bot's purpose |
   | **about url** | Optional - can leave blank or add GitHub repo |
   | **redirect uri** | `http://localhost:8080` (required field but not used) |

4. Click **"create app"**

### Save Your Credentials

After creation, you'll see your app details. **Save these immediately:**

- **Client ID**: 14-character string displayed under your app name
  - Example: `p-jcoLKBynTLew`
- **Client Secret**: ~27-character string next to "secret"
  - Example: `gko_LXELoV07ZBNUXrvWZfzE3aI`

⚠️ **Never share or commit these credentials!**

---

## 2. Understanding Script App Authentication

### OAuth2 Password Grant Flow

Script apps use the **password grant** type of OAuth2:

1. Your application sends Reddit username + password to Reddit
2. Reddit validates credentials and returns an access token
3. You use the access token for all subsequent API calls
4. Token expires after 1 hour - you must re-authenticate

### Required OAuth Scopes

For a posting bot, you need these scopes:

| Scope | Purpose |
|-------|---------|
| `submit` | Create posts and comments |
| `identity` | Verify account information |
| `read` | Read posts and subreddit content (for verification) |

**Note**: For script apps, you typically get scope `*` (all permissions) by default.

### Token Expiration

- **Lifetime**: 3600 seconds (exactly 1 hour)
- **Refresh**: Script apps do NOT support refresh tokens
- **Solution**: Request a new token when expired (track expiration time)

---

## 3. Obtain an Access Token

### Token Request Details

**Endpoint**: `https://www.reddit.com/api/v1/access_token`
**Method**: POST
**Authentication**: HTTP Basic Auth (client_id:client_secret)

### Request Format

**Headers**:
```http
Authorization: Basic [base64(client_id:client_secret)]
User-Agent: platform:app_name:version (by /u/your_reddit_username)
Content-Type: application/x-www-form-urlencoded
```

**Body** (form-encoded):
```
grant_type=password&username=REDDIT_USERNAME&password=REDDIT_PASSWORD
```

⚠️ **User-Agent Format is Mandatory**: Must follow the pattern:
- `platform:app_name:version (by /u/username)`
- Example: `windows:ATWFanBot:v1.0 (by /u/YourRedditUsername)`
- Reddit will return `429 Too Many Requests` if User-Agent is missing or improperly formatted

### Example: curl

```bash
curl -X POST "https://www.reddit.com/api/v1/access_token" \
  -u "YOUR_CLIENT_ID:YOUR_CLIENT_SECRET" \
  -H "User-Agent: windows:ATWFanBot:v1.0 (by /u/YourUsername)" \
  -d "grant_type=password&username=your_reddit_username&password=your_reddit_password"
```

### Successful Response

```json
{
  "access_token": "eyJhbGciOiJSUzI1NiIsImtpZCI6IlNIQTI1Nj...",
  "token_type": "bearer",
  "expires_in": 3600,
  "scope": "*"
}
```

**Response Fields**:
- `access_token`: The bearer token (use for API calls)
- `token_type`: Always "bearer"
- `expires_in`: Seconds until expiration (3600 = 1 hour)
- `scope`: Granted permissions (`*` = all scopes)

### Error Response

```json
{
  "error": "invalid_grant"
}
```

**Common errors**:
- `invalid_grant` - Wrong username/password, or 2FA is enabled
- `401 Unauthorized` - Invalid client_id or client_secret
- `429 Too Many Requests` - Missing/invalid User-Agent or rate limited

---

## 4. Use the Token for API Calls

### API Base URL

⚠️ **Important**: Use `https://oauth.reddit.com` (NOT `www.reddit.com`) for authenticated requests.

### Required Headers

**Every API request must include**:
```http
Authorization: bearer YOUR_ACCESS_TOKEN
User-Agent: platform:app_name:version (by /u/username)
```

### Example: Verify Identity

Test your token by getting your account info:

```bash
curl -H "Authorization: bearer YOUR_ACCESS_TOKEN" \
  -H "User-Agent: windows:ATWFanBot:v1.0 (by /u/YourUsername)" \
  https://oauth.reddit.com/api/v1/me
```

**Response**:
```json
{
  "name": "YourUsername",
  "id": "t2_abc123",
  "link_karma": 1234,
  "comment_karma": 5678,
  ...
}
```

### Example: Submit a Post

Create a self-post (text post):

```bash
curl -X POST "https://oauth.reddit.com/api/submit" \
  -H "Authorization: bearer YOUR_ACCESS_TOKEN" \
  -H "User-Agent: windows:ATWFanBot:v1.0 (by /u/YourUsername)" \
  -d "sr=test&kind=self&title=Test Post&text=This is a test post&api_type=json&sendreplies=false"
```

**Parameters**:
- `sr`: Subreddit name (without "r/")
- `kind`: Post type (`self` for text, `link` for URL)
- `title`: Post title (max 300 characters)
- `text`: Post body in markdown
- `api_type`: Set to `json` for JSON response
- `sendreplies`: Set to `false` to disable inbox replies

**Success Response**:
```json
{
  "json": {
    "errors": [],
    "data": {
      "id": "abc123",
      "name": "t3_abc123",
      "url": "https://reddit.com/r/test/comments/abc123/..."
    }
  }
}
```

### Example: Verify Post Exists

After posting, verify it was created:

```bash
curl -H "Authorization: bearer YOUR_ACCESS_TOKEN" \
  -H "User-Agent: windows:ATWFanBot:v1.0 (by /u/YourUsername)" \
  "https://oauth.reddit.com/api/info?id=t3_abc123"
```

---

## 5. Rate Limits

### OAuth Rate Limits

**Authenticated requests (OAuth)**:
- **60-100 requests per minute** (sources vary; assume 60 to be safe)
- Calculated as a **rolling average over 10 minutes**
- Allows bursts above the per-minute limit as long as average stays below threshold

**Unauthenticated requests**:
- **10 requests per minute** (very limited)
- Always use OAuth authentication for production bots

### Rate Limit Headers

Reddit includes rate limit info in response headers:

```
X-Ratelimit-Used: 15.0
X-Ratelimit-Remaining: 45.0
X-Ratelimit-Reset: 1234567890
```

### Handling Rate Limits

**429 Too Many Requests**:
- Response includes `Retry-After` header (seconds to wait)
- **Best practice**: Implement exponential backoff
- Track requests and stay well below limits

**For ATWFanBot** (daily posting):
- 1 post per day = ~4 requests total (auth, submit, verify, health check)
- Rate limiting should never be an issue with this usage pattern

---

## 6. Common Errors & Troubleshooting

### Authentication Errors

| Error | Cause | Solution |
|-------|-------|----------|
| `invalid_grant` | Wrong username/password | Verify credentials are correct |
| `invalid_grant` | 2FA is enabled | Disable 2FA on the Reddit account |
| `401 Unauthorized` | Wrong client_id/secret | Check app credentials at reddit.com/prefs/apps |
| `429 Too Many Requests` | Missing User-Agent | Add proper User-Agent header |
| `403 Forbidden` | Account banned/suspended | Check account status |

### Posting Errors

| Error | Cause | Solution |
|-------|-------|----------|
| `SUBREDDIT_NOEXIST` | Subreddit doesn't exist | Check subreddit name spelling |
| `SUBREDDIT_NOTALLOWED` | Bot banned from subreddit | Contact subreddit moderators |
| `RATELIMIT` | Posting too frequently | Wait before posting again (usually 10 minutes for new accounts) |
| `NO_TEXT` | Empty post body | Ensure text content is provided |

### Debugging Tips

1. **Test with curl first** - Validate API calls before coding
2. **Check User-Agent format** - Most common issue for 429 errors
3. **Verify token expiration** - Tokens last exactly 1 hour
4. **Test on r/test** - Use the test subreddit before production
5. **Enable verbose logging** - Log all HTTP requests/responses

---

## 7. Best Practices

### Security

✅ **DO**:
- Store credentials in environment variables (never in code)
- Use HTTPS for all requests
- Rotate credentials every 90 days
- Use a dedicated Reddit account for bots
- Set restrictive file permissions on credential files

❌ **DON'T**:
- Commit secrets to version control
- Log credentials (even in debug mode)
- Share client_secret publicly
- Use your personal Reddit account for bots
- Hard-code credentials in source files

### Performance

- **Cache access tokens** - Reuse for the full 1-hour lifetime
- **Track expiration** - Request new token 60 seconds before expiry
- **Batch operations** - Minimize API calls where possible
- **Respect rate limits** - Stay well below 60 requests/minute

### Reliability

- **Implement retries** - Use exponential backoff for transient errors
- **Handle rate limits** - Check `Retry-After` header on 429 responses
- **Verify posts** - Confirm post exists after submission
- **Log everything** - Detailed logging helps debug issues
- **Idempotency** - Track posted content to prevent duplicates

### Reddit Community Guidelines

- **Be transparent** - Clearly identify your bot
- **Follow subreddit rules** - Check posting rules before automating
- **Rate limit yourself** - Don't spam even if technically allowed
- **Respond to reports** - Monitor and address issues quickly
- **Add flair** - Some subreddits require bots to have flair

---

## 8. .NET Implementation

### Minimal Token Request Example

```csharp
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

public async Task<string> GetRedditAccessToken(
    string clientId,
    string clientSecret,
    string username,
    string password)
{
    using var client = new HttpClient();

    // Set User-Agent (REQUIRED)
    client.DefaultRequestHeaders.Add("User-Agent",
        "windows:ATWFanBot:v1.0 (by /u/YourUsername)");

    // Set Basic Authentication
    var credentials = Convert.ToBase64String(
        Encoding.ASCII.GetBytes($"{clientId}:{clientSecret}"));
    client.DefaultRequestHeaders.Authorization =
        new AuthenticationHeaderValue("Basic", credentials);

    // Prepare form data
    var formData = new Dictionary<string, string>
    {
        { "grant_type", "password" },
        { "username", username },
        { "password", password }
    };
    var content = new FormUrlEncodedContent(formData);

    // Request token
    var response = await client.PostAsync(
        "https://www.reddit.com/api/v1/access_token",
        content);

    response.EnsureSuccessStatusCode();

    var json = await response.Content.ReadAsStringAsync();
    var tokenResponse = JsonSerializer.Deserialize<TokenResponse>(json);

    return tokenResponse.AccessToken;
}

public class TokenResponse
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; }

    [JsonPropertyName("token_type")]
    public string TokenType { get; set; }

    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; }

    [JsonPropertyName("scope")]
    public string Scope { get; set; }
}
```

### Full Implementation

The ATWFanBot project includes a complete implementation:
- **`Services/RedditApiClient.cs`** - Full OAuth + API client with token caching
- Token expiration tracking and automatic refresh
- Post submission and verification
- Comprehensive error handling

See the main project files for production-ready code.

---

## Additional Resources

### Official Documentation
- **Reddit API**: https://www.reddit.com/dev/api
- **OAuth2 Guide**: https://github.com/reddit-archive/reddit/wiki/OAuth2
- **API Rules**: https://github.com/reddit-archive/reddit/wiki/API

### Community Resources
- **r/redditdev** - Subreddit for API developers
- **PRAW Documentation** - Python Reddit API Wrapper (good reference)
- **reddit.com/r/test** - Test subreddit for bot development

### Rate Limit & API Guidelines
- Reddit Data API Wiki: https://support.reddithelp.com/hc/en-us/articles/16160319875092
- Bottiquette: https://www.reddit.com/wiki/bottiquette

---

## Summary Checklist

Before going live with your bot:

- [ ] Registered script app and saved client_id + client_secret
- [ ] Verified Reddit account does NOT have 2FA enabled
- [ ] Implemented proper User-Agent format
- [ ] Token caching and expiration handling implemented
- [ ] Retry logic with exponential backoff added
- [ ] Post verification after submission
- [ ] Tested on r/test or private subreddit
- [ ] All credentials in environment variables (not code)
- [ ] Logging configured for debugging
- [ ] Rate limiting respected (well under 60 req/min)
- [ ] Idempotency checks to prevent duplicate posts

---

**Last Updated**: January 2026
**ATWFanBot Version**: 1.0
