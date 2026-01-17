Getting started with Reddit API (concise checklist)

1) Register an app (done via https://www.reddit.com/prefs/apps)
- Create app type `script` and save `client id` and `client secret`.
- Set Redirect URI (e.g. `http://localhost:8080`) — required but not used for `script` apps.

2) Decide auth & scopes
- For a daily posting bot the `script` app + OAuth2 password grant is the common approach.
- Required scopes for posting and validation: `submit`, `identity`, `read`.

3) Obtain an access token
- Token endpoint: `https://www.reddit.com/api/v1/access_token` (use HTTP Basic auth with `client_id:client_secret`).
- Request body (form): `grant_type=password&username=REDDIT_USERNAME&password=REDDIT_PASSWORD&scope=submit,identity,read`

Example curl to get a token:

```
curl -u "CLIENT_ID:CLIENT_SECRET" \
  -X POST "https://www.reddit.com/api/v1/access_token" \
  -d "grant_type=password&username=YOUR_USERNAME&password=YOUR_PASSWORD&scope=submit,identity,read" \
  -H "User-Agent: ATWFanBot/1.0 by YOUR_REDDIT_USERNAME"
```

4) Use the token for API calls
- Use `https://oauth.reddit.com` as the base for authenticated calls.
- Include headers:
  - `Authorization: bearer ACCESS_TOKEN`
  - `User-Agent: YourAppName/Version by reddit_username` (must be descriptive)

Verify identity (example):
```
curl -H "Authorization: bearer ACCESS_TOKEN" \
  -H "User-Agent: ATWFanBot/1.0 by YOUR_REDDIT_USERNAME" \
  https://oauth.reddit.com/api/v1/me
```

Submit a self-post (example):
```
curl -X POST "https://oauth.reddit.com/api/submit" \
  -H "Authorization: bearer ACCESS_TOKEN" \
  -H "User-Agent: ATWFanBot/1.0 by YOUR_REDDIT_USERNAME" \
  -d "sr=SUBREDDIT&kind=self&title=Post%20Title&text=Post%20body%20markdown&api_type=json"
```

Notes and best practices
- Access tokens typically expire in ~1 hour; re-request tokens as needed.
- Respect rate limits; include a clear `User-Agent` string.
- Test on a private subreddit before posting to production.
- Never commit `client_secret`, `username`, or `password`; use environment variables or a secrets store.

.NET (HttpClient) minimal flow

- Request token with `HttpClient` using Basic auth and form-encoded body.
- Use returned `access_token` in `Authorization` header for subsequent requests.

Minimal snippet (conceptual):

```
// get token
var client = new HttpClient();
var auth = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}"));
client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", auth);
var form = new Dictionary<string,string> {
  {"grant_type","password"},
  {"username", username},
  {"password", password},
  {"scope","submit identity read"}
};
var tokenResp = await client.PostAsync("https://www.reddit.com/api/v1/access_token", new FormUrlEncodedContent(form));
// parse access_token from JSON and use in subsequent requests to https://oauth.reddit.com
```

If you want, I can add a fully-working C# example file suitable for this project (reads env vars, requests token, submits a test post).