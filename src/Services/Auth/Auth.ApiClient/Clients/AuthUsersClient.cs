using System.Net;
using System.Net.Http.Json;
using LayeredTemplate.Auth.ApiClient.Models;

namespace LayeredTemplate.Auth.ApiClient.Clients;

internal sealed class AuthUsersClient : IAuthUsersClient
{
    private readonly HttpClient http;

    public AuthUsersClient(HttpClient http)
    {
        this.http = http;
    }

    public async Task<UserResponse?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        using var response = await this.http.GetAsync(
            $"api/admin/users/{Uri.EscapeDataString(id)}",
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<UserResponse>(cancellationToken);
    }

    public async Task<UserResponse?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        using var response = await this.http.GetAsync(
            $"api/admin/users?email={Uri.EscapeDataString(email)}",
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<UserResponse>(cancellationToken);
    }

    public async Task<UserResponse> CreateAsync(CreateUserRequest request, CancellationToken cancellationToken = default)
    {
        using var response = await this.http.PostAsJsonAsync(
            "api/admin/users",
            request,
            cancellationToken);

        await EnsureSuccessAsync(response, cancellationToken);
        return (await response.Content.ReadFromJsonAsync<UserResponse>(cancellationToken))!;
    }

    public async Task<UserResponse> UpdateAsync(string id, UpdateUserRequest request, CancellationToken cancellationToken = default)
    {
        using var response = await this.http.PatchAsJsonAsync(
            $"api/admin/users/{Uri.EscapeDataString(id)}",
            request,
            cancellationToken);

        await EnsureSuccessAsync(response, cancellationToken);
        return (await response.Content.ReadFromJsonAsync<UserResponse>(cancellationToken))!;
    }

    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        using var response = await this.http.DeleteAsync(
            $"api/admin/users/{Uri.EscapeDataString(id)}",
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return;
        }

        await EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task<InviteTokenResponse> CreateInviteTokenAsync(string id, CancellationToken cancellationToken = default)
    {
        using var response = await this.http.PostAsync(
            $"api/admin/users/{Uri.EscapeDataString(id)}/invite-token",
            content: null,
            cancellationToken);

        await EnsureSuccessAsync(response, cancellationToken);
        return (await response.Content.ReadFromJsonAsync<InviteTokenResponse>(cancellationToken))!;
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        ProblemPayload? problem = null;
        var contentType = response.Content.Headers.ContentType?.MediaType;
        // ASP.NET Core returns ProblemDetails as application/problem+json; be lenient and also accept application/json.
        if (string.Equals(contentType, "application/problem+json", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(contentType, "application/json", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                problem = await response.Content.ReadFromJsonAsync<ProblemPayload>(cancellationToken);
            }
            catch
            {
                // Ignore JSON parse failure — fall through to reason-phrase message.
            }
        }

        // Message preference: detail > title > reason phrase > generic status. `detail` is per-occurrence,
        // `title` is the problem-type summary — either is suitable for Exception.Message.
        var message = problem?.Detail
            ?? problem?.Title
            ?? response.ReasonPhrase
            ?? $"Auth API returned {(int)response.StatusCode}.";

        throw new AuthApiException(
            response.StatusCode,
            message,
            title: problem?.Title,
            detail: problem?.Detail,
            errors: problem?.Errors);
    }

    /// <summary>Internal mirror of RFC 7807 <c>ProblemDetails</c> / <c>ValidationProblemDetails</c>.</summary>
    private sealed record ProblemPayload(
        string? Title,
        string? Detail,
        IReadOnlyDictionary<string, string[]>? Errors);
}
