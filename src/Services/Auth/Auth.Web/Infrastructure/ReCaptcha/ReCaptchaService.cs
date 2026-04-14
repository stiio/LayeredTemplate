using System.Text.Json;
using Microsoft.Extensions.Options;

namespace LayeredTemplate.Auth.Web.Infrastructure.ReCaptcha;

public class ReCaptchaService
{
    private const string VerifyUrl = "https://www.google.com/recaptcha/api/siteverify";

    private readonly HttpClient httpClient;
    private readonly ReCaptchaSettings settings;

    public ReCaptchaService(HttpClient httpClient, IOptions<ReCaptchaSettings> options)
    {
        this.httpClient = httpClient;
        this.settings = options.Value;
    }

    public bool IsEnabled => !string.IsNullOrEmpty(this.settings.SiteKey)
                             && !string.IsNullOrEmpty(this.settings.SecretKey);

    public async Task<bool> ValidateAsync(string? token)
    {
        if (!this.IsEnabled)
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["secret"] = this.settings.SecretKey,
            ["response"] = token,
        });

        var response = await this.httpClient.PostAsync(VerifyUrl, content);
        if (!response.IsSuccessStatusCode)
        {
            return false;
        }

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        return json.TryGetProperty("success", out var success) && success.GetBoolean();
    }

    public async Task<bool> ValidateAsync(HttpContext httpContext)
    {
        if (!this.IsEnabled)
        {
            return true;
        }

        var token = httpContext.Request.HasFormContentType
            ? httpContext.Request.Form["g-recaptcha-response"].ToString()
            : null;

        return await this.ValidateAsync(token);
    }
}
