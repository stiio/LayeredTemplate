namespace LayeredTemplate.Shared.Options;

public class SmtpSettings
{
    public string From { get; set; } = null!;

    public string Host { get; set; } = null!;

    public int Port { get; set; }

    public string User { get; set; } = null!;

    public string Password { get; set; } = null!;
}