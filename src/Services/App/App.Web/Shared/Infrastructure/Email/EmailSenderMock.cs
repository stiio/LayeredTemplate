namespace LayeredTemplate.App.Shared.Infrastructure.Email;

internal sealed class EmailSenderMock : IEmailSender
{
    private readonly ILogger<EmailSenderMock> logger;

    public EmailSenderMock(ILogger<EmailSenderMock> logger)
    {
        this.logger = logger;
    }

    public Task SendEmail(string to, string subject, string htmlBody, CancellationToken cancellationToken = default) =>
        this.SendEmail([to], subject, htmlBody, cancellationToken);

    public Task SendEmail(string[] tos, string subject, string htmlBody, CancellationToken cancellationToken = default)
    {
        this.logger.LogInformation(
            "[MockEmailSender] To: {Recipients} | Subject: {Subject} | Body: {Body}",
            string.Join(", ", tos),
            subject,
            htmlBody);
        return Task.CompletedTask;
    }
}