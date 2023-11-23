using System.Text;
using System.Text.Json;

namespace LayeredTemplate.Web.Extensions;

public static class ConfigurationBuilderExtensions
{
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