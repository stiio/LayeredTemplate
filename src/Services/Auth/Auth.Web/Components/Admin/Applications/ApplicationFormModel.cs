using System.ComponentModel.DataAnnotations;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace LayeredTemplate.Auth.Web.Components.Admin.Applications;

/// <summary>
/// UI model for creating/editing an OpenIddict application.
/// Mapped to/from <see cref="OpenIddictApplicationDescriptor"/> via the static helpers below.
/// </summary>
public class ApplicationFormModel
{
    [Required]
    [MaxLength(100)]
    public string ClientId { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? DisplayName { get; set; }

    public ClientTypeOption ClientType { get; set; } = ClientTypeOption.Confidential;

    /// <summary>Newline-separated list of absolute URIs.</summary>
    public string RedirectUris { get; set; } = string.Empty;

    /// <summary>Newline-separated list of absolute URIs.</summary>
    public string PostLogoutRedirectUris { get; set; } = string.Empty;

    public bool AllowAuthorizationCode { get; set; } = true;

    public bool AllowRefreshToken { get; set; } = true;

    public bool AllowClientCredentials { get; set; }

    public bool AllowPassword { get; set; }

    public bool ScopeOpenid { get; set; } = true;

    public bool ScopeProfile { get; set; } = true;

    public bool ScopeEmail { get; set; } = true;

    /// <summary>Newline-separated list of extra scope names (without the scp: prefix).</summary>
    public string? CustomScopes { get; set; }

    public bool RequirePkce { get; set; } = true;

    public static ApplicationFormModel FromDescriptor(OpenIddictApplicationDescriptor d)
    {
        var model = new ApplicationFormModel
        {
            ClientId = d.ClientId ?? string.Empty,
            DisplayName = d.DisplayName,
            ClientType = string.Equals(d.ClientType, ClientTypes.Public, StringComparison.OrdinalIgnoreCase)
                ? ClientTypeOption.Public
                : ClientTypeOption.Confidential,
            RedirectUris = string.Join("\n", d.RedirectUris.Select(u => u.ToString())),
            PostLogoutRedirectUris = string.Join("\n", d.PostLogoutRedirectUris.Select(u => u.ToString())),
            AllowAuthorizationCode = d.Permissions.Contains(Permissions.GrantTypes.AuthorizationCode),
            AllowRefreshToken = d.Permissions.Contains(Permissions.GrantTypes.RefreshToken),
            AllowClientCredentials = d.Permissions.Contains(Permissions.GrantTypes.ClientCredentials),
            AllowPassword = d.Permissions.Contains(Permissions.GrantTypes.Password),
            ScopeOpenid = d.Permissions.Contains(Permissions.Prefixes.Scope + "openid"),
            ScopeProfile = d.Permissions.Contains(Permissions.Prefixes.Scope + "profile"),
            ScopeEmail = d.Permissions.Contains(Permissions.Prefixes.Scope + "email"),
            RequirePkce = d.Requirements.Contains(Requirements.Features.ProofKeyForCodeExchange),
        };

        var knownScopes = new[] { "openid", "profile", "email" };
        var customScopes = d.Permissions
            .Where(p => p.StartsWith(Permissions.Prefixes.Scope, StringComparison.Ordinal))
            .Select(p => p[Permissions.Prefixes.Scope.Length..])
            .Where(s => !knownScopes.Contains(s, StringComparer.Ordinal))
            .ToList();
        model.CustomScopes = customScopes.Count > 0 ? string.Join("\n", customScopes) : null;

        return model;
    }

    public void ApplyTo(OpenIddictApplicationDescriptor d)
    {
        d.ClientId = this.ClientId.Trim();
        d.DisplayName = string.IsNullOrWhiteSpace(this.DisplayName) ? null : this.DisplayName.Trim();
        d.ClientType = this.ClientType == ClientTypeOption.Public ? ClientTypes.Public : ClientTypes.Confidential;

        d.RedirectUris.Clear();
        foreach (var uri in ParseUris(this.RedirectUris))
        {
            d.RedirectUris.Add(uri);
        }

        d.PostLogoutRedirectUris.Clear();
        foreach (var uri in ParseUris(this.PostLogoutRedirectUris))
        {
            d.PostLogoutRedirectUris.Add(uri);
        }

        d.Permissions.Clear();

        if (this.AllowAuthorizationCode)
        {
            d.Permissions.Add(Permissions.GrantTypes.AuthorizationCode);
            d.Permissions.Add(Permissions.ResponseTypes.Code);
            d.Permissions.Add(Permissions.Endpoints.Authorization);
            d.Permissions.Add(Permissions.Endpoints.Token);
            d.Permissions.Add(Permissions.Endpoints.EndSession);
        }

        if (this.AllowRefreshToken)
        {
            d.Permissions.Add(Permissions.GrantTypes.RefreshToken);
        }

        if (this.AllowClientCredentials)
        {
            d.Permissions.Add(Permissions.GrantTypes.ClientCredentials);
            d.Permissions.Add(Permissions.Endpoints.Token);
        }

        if (this.AllowPassword)
        {
            d.Permissions.Add(Permissions.GrantTypes.Password);
            d.Permissions.Add(Permissions.Endpoints.Token);
        }

        if (this.ScopeOpenid)
        {
            d.Permissions.Add(Permissions.Prefixes.Scope + "openid");
        }

        if (this.ScopeProfile)
        {
            d.Permissions.Add(Permissions.Prefixes.Scope + "profile");
        }

        if (this.ScopeEmail)
        {
            d.Permissions.Add(Permissions.Prefixes.Scope + "email");
        }

        foreach (var scope in ParseLines(this.CustomScopes))
        {
            d.Permissions.Add(Permissions.Prefixes.Scope + scope);
        }

        d.Requirements.Clear();
        if (this.RequirePkce)
        {
            d.Requirements.Add(Requirements.Features.ProofKeyForCodeExchange);
        }
    }

    private static IEnumerable<Uri> ParseUris(string? raw) =>
        ParseLines(raw)
            .Select(line => Uri.TryCreate(line, UriKind.Absolute, out var uri) ? uri : null)
            .Where(u => u is not null)
            .Select(u => u!);

    private static IEnumerable<string> ParseLines(string? raw) =>
        string.IsNullOrWhiteSpace(raw)
            ? []
            : raw.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}

public enum ClientTypeOption
{
    Public,
    Confidential,
}
