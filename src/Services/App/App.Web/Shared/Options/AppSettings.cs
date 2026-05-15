namespace LayeredTemplate.App.Shared.Options;

public sealed class AppSettings
{
}

public sealed class SmtpSettings
{
    public string From { get; set; } = null!;

    public string Host { get; set; } = null!;

    public int Port { get; set; }

    public string User { get; set; } = null!;

    public string Password { get; set; } = null!;
}

public static class ConnectionStringKeys
{
    public const string WriteDb = "ConnectionStrings:AppWriteDbConnection";

    public const string ReadDb = "ConnectionStrings:AppReadDbConnection";
}
