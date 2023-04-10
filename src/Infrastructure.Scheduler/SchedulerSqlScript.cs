using System.Reflection;

namespace LayeredTemplate.Infrastructure.Scheduler;

public static class SchedulerSqlScript
{
    public static string Get()
    {
        var assembly = Assembly.GetAssembly(typeof(SchedulerSqlScript))!;

        var resourceNames = assembly.GetManifestResourceNames();
        var resourceName = resourceNames.First(name => name.EndsWith("quartz_tables_script_psql.sql"));

        using var stream = assembly.GetManifestResourceStream(resourceName);
        using var reader = new StreamReader(stream!);

        return reader.ReadToEnd();
    }
}