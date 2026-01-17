using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using ATWFanBot.Configuration;
using ATWFanBot.Models;
using Serilog;

namespace ATWFanBot.Services;

public class RedditApiClient
{
    private readonly HttpClient _httpClient;
    private readonly RedditSettings _settings;
    private readonly Secrets _secrets;
    private string? _accessToken;
    private DateTime _tokenExpiry;

    public RedditApiClient(RedditSettings settings, Secrets secrets)
    {
        _settings = settings;
        _secrets = secrets;
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("User-Agent", _settings.UserAgent);
    }

    public async Task<string> GetAccessTokenAsync()
    {
        // Return cached token if still valid
        if (!string.IsNullOrEmpty(_accessToken) && DateTime.UtcNow < _tokenExpiry)
        {
            return _accessToken;
        }

        Log.Information("Obtaining Reddit OAuth access token");

        var credentials = Convert.ToBase64String(
            Encoding.ASCII.GetBytes($"{_secrets.RedditClientId}:{_secrets.RedditClientSecret}")
        );

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://www.reddit.com/api/v1/access_token");
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
        request.Headers.Add("User-Agent", _settings.UserAgent);

        var content = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("grant_type", "password"),
            new KeyValuePair<string, string>("username", _secrets.RedditUsername),
            new KeyValuePair<string, string>("password", _secrets.RedditPassword)
        });

        request.Content = content;

        var response = await _httpClient.SendAsync(request);
        var jsonResponse = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            Log.Error("Failed to obtain access token. Status: {StatusCode}, Response: {Response}",
                response.StatusCode, jsonResponse);
            throw new HttpRequestException(
                $"Failed to obtain Reddit access token: {response.StatusCode}. Response: {jsonResponse}");
        }

        var tokenResponse = JsonSerializer.Deserialize<RedditTokenResponse>(jsonResponse);
        if (tokenResponse == null || string.IsNullOrEmpty(tokenResponse.AccessToken))
        {
            throw new InvalidOperationException("Failed to parse access token from response");
        }

        _accessToken = tokenResponse.AccessToken;
        _tokenExpiry = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn - 60); // Refresh 60 seconds early

        Log.Information("Successfully obtained access token, expires in {ExpiresIn} seconds", tokenResponse.ExpiresIn);

        return _accessToken;
    }

    public async Task<(string postId, string postUrl)> SubmitPostAsync(string subreddit, string title, string body)
    {
        var token = await GetAccessTokenAsync();

        Log.Information("Submitting post to r/{Subreddit}: {Title}", subreddit, title);

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://oauth.reddit.com/api/submit");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Add("User-Agent", _settings.UserAgent);

        var content = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("sr", subreddit),
            new KeyValuePair<string, string>("kind", "self"),
            new KeyValuePair<string, string>("title", title),
            new KeyValuePair<string, string>("text", body),
            new KeyValuePair<string, string>("api_type", "json"),
            new KeyValuePair<string, string>("sendreplies", "false")
        });

        request.Content = content;

        var response = await _httpClient.SendAsync(request);
        var jsonResponse = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            Log.Error("Failed to submit post. Status: {StatusCode}, Response: {Response}",
                response.StatusCode, jsonResponse);
            throw new HttpRequestException(
                $"Failed to submit post: {response.StatusCode}. Response: {jsonResponse}");
        }

        var submitResponse = JsonSerializer.Deserialize<RedditSubmitResponse>(jsonResponse);

        if (submitResponse?.Json?.Errors != null && submitResponse.Json.Errors.Count > 0)
        {
            var errorsJson = JsonSerializer.Serialize(submitResponse.Json.Errors);
            Log.Error("Reddit API returned errors: {Errors}", errorsJson);
            throw new InvalidOperationException($"Reddit API returned errors: {errorsJson}");
        }

        var postData = submitResponse?.Json?.Data;
        if (postData == null || string.IsNullOrEmpty(postData.Id))
        {
            throw new InvalidOperationException($"Failed to extract post ID from response: {jsonResponse}");
        }

        var postId = postData.Id;
        var postUrl = postData.Url ?? $"https://reddit.com/r/{subreddit}/comments/{postId}";

        Log.Information("Successfully submitted post. ID: {PostId}, URL: {PostUrl}", postId, postUrl);

        return (postId, postUrl);
    }

    public async Task<bool> VerifyPostAsync(string postId)
    {
        var token = await GetAccessTokenAsync();

        Log.Information("Verifying post exists: {PostId}", postId);

        using var request = new HttpRequestMessage(HttpMethod.Get,
            $"https://oauth.reddit.com/api/info?id=t3_{postId}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Add("User-Agent", _settings.UserAgent);

        var response = await _httpClient.SendAsync(request);

        if (!response.IsSuccessStatusCode)
        {
            Log.Warning("Post verification request failed. Status: {StatusCode}", response.StatusCode);
            return false;
        }

        var jsonResponse = await response.Content.ReadAsStringAsync();

        // Check if the response contains the post data
        var isValid = jsonResponse.Contains($"\"id\": \"t3_{postId}\"") ||
                      jsonResponse.Contains($"\"name\": \"t3_{postId}\"");

        if (isValid)
        {
            Log.Information("Post verified successfully: {PostId}", postId);
        }
        else
        {
            Log.Warning("Post verification failed - post not found: {PostId}", postId);
        }

        return isValid;
    }
}
