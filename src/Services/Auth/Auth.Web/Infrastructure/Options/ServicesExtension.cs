using System.Text;
using System.Text.Json;
using LayeredTemplate.Auth.Web.Infrastructure.Options.Models;

namespace LayeredTemplate.Auth.Web.Infrastructure.Options;

public static class ServicesExtension
{
    public static void AddAppSettings(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<AppSettings>(configuration.GetSection(nameof(AppSettings)));
        services.Configure<SmtpSettings>(configuration.GetSection(nameof(SmtpSettings)));
        services.Configure<ReCaptchaSettings>(configuration.GetSection(nameof(ReCaptchaSettings)));
    }

    public static IConfigurationBuilder AddEnvironmentVariablesFromJsonVariables(this IConfigurationBuilder configurationBuilder)
    {
        var jsonSettingsNamesRaw = Environment.GetEnvironmentVariable("json_settings_names");
        if (string.IsNullOrEmpty(jsonSettingsNamesRaw))
        {
            return configurationBuilder;
        }

        var jsonSettingsNames = JsonSerializer.Deserialize<string[]>(jsonSettingsNamesRaw)!;

        foreach (var jsonSettingsName in jsonSettingsNames)
        {
            var settings = Environment.GetEnvironmentVariable(jsonSettingsName);
            if (string.IsNullOrEmpty(settings))
            {
                continue;
            }

            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(settings));
            configurationBuilder.AddJsonStream(stream);
        }

        return configurationBuilder;
    }
}