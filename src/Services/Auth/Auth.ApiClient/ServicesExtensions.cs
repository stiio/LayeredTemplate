using LayeredTemplate.Auth.ApiClient.Clients;
using LayeredTemplate.Auth.ApiClient.Handlers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace LayeredTemplate.Auth.ApiClient;

public static class ServicesExtensions
{
    /// <summary>Default config section name (<c>AuthApiClient</c>).</summary>
    public const string DefaultConfigSection = "AuthApiClient";

    /// <summary>
    /// Registers the Auth.Web admin API client. Options are loaded from <paramref name="configuration"/>
    /// (section <c>AuthApiClient</c> by default), then <paramref name="configure"/> is applied on top —
    /// use it to override any value programmatically.
    /// </summary>
    public static IHttpClientBuilder AddAuthApiClient(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<AuthApiClientOptions>? configure = null,
        string configSection = DefaultConfigSection)
    {
        var optionsBuilder = services
            .AddOptions<AuthApiClientOptions>()
            .Bind(configuration.GetSection(configSection));

        if (configure is not null)
        {
            optionsBuilder.Configure(configure);
        }

        optionsBuilder
            .Validate(o => !string.IsNullOrWhiteSpace(o.BaseUrl), "AuthApiClient:BaseUrl is required.")
            .Validate(o => Uri.TryCreate(o.BaseUrl, UriKind.Absolute, out _), "AuthApiClient:BaseUrl must be an absolute URI.")
            .Validate(o => !string.IsNullOrWhiteSpace(o.ClientId), "AuthApiClient:ClientId is required.")
            .Validate(o => !string.IsNullOrWhiteSpace(o.ClientSecret), "AuthApiClient:ClientSecret is required.")
            .Validate(o => o.Scopes.Length > 0, "AuthApiClient:Scopes must contain at least one scope.")
            .ValidateOnStart();

        // Dedicated HttpClient for hitting /connect/token — no auth handler attached
        // (would recurse: handler → token fetch → handler → ...).
        services.AddHttpClient(AccessTokenClientName);

        services.AddSingleton<AccessTokenProvider>();
        services.AddTransient<ClientCredentialsHandler>();

        return services.AddHttpClient<IAuthUsersClient, AuthUsersClient>((sp, client) =>
            {
                var options = sp.GetRequiredService<IOptions<AuthApiClientOptions>>().Value;
                client.BaseAddress = new Uri(options.BaseUrl);
            })
            .AddHttpMessageHandler<ClientCredentialsHandler>();
    }

    internal const string AccessTokenClientName = "auth-token";
}
