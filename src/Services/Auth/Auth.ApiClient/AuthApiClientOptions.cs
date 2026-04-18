namespace LayeredTemplate.Auth.ApiClient;

public class AuthApiClientOptions
{
    /// <summary>Base URL of Auth.Web (e.g. <c>https://auth.example.com</c>). Used as HttpClient BaseAddress and to resolve <c>/connect/token</c>.</summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>OIDC client_id — must be a confidential client with the <c>client_credentials</c> grant.</summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>Plain client secret.</summary>
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>Scopes requested on every token fetch. Must be a subset of the client's permissions in Auth.Web.</summary>
    public string[] Scopes { get; set; } = [];

    /// <summary>How early the token cache considers a token "about to expire" and refreshes proactively. Default 60s.</summary>
    public TimeSpan TokenRefreshBuffer { get; set; } = TimeSpan.FromSeconds(60);
}
