using System.Text.Json.Serialization;

namespace ATWFanBot.Models;

public class RedditTokenResponse
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; } = string.Empty;

    [JsonPropertyName("token_type")]
    public string TokenType { get; set; } = string.Empty;

    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; }

    [JsonPropertyName("scope")]
    public string Scope { get; set; } = string.Empty;
}

public class RedditSubmitResponse
{
    [JsonPropertyName("json")]
    public RedditSubmitData? Json { get; set; }
}

public class RedditSubmitData
{
    [JsonPropertyName("errors")]
    public List<object>? Errors { get; set; }

    [JsonPropertyName("data")]
    public RedditPostData? Data { get; set; }
}

public class RedditPostData
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("url")]
    public string? Url { get; set; }
}

public class RedditApiError
{
    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("error")]
    public int Error { get; set; }
}
