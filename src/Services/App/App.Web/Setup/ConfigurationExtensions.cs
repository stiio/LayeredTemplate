using System.Text;
using System.Text.Json;

namespace LayeredTemplate.App.Setup;

public static class ConfigurationExtensions
{
    /// <summary>
    /// Reads the <c>json_settings_names</c> env var (a JSON array of variable names), pulls the
    /// JSON value of each, and merges it into the configuration. Useful for shipping a single
    /// secrets blob per environment (e.g. AWS Secrets Manager) instead of dozens of vars.
    /// </summary>
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
