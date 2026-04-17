namespace LayeredTemplate.Auth.Web.Infrastructure.Email.Services;

public class MockEmailSender : IEmailSender
{
    private readonly ILogger<MockEmailSender> logger;

    public MockEmailSender(ILogger<MockEmailSender> logger)
    {
        this.logger = logger;
    }

    public Task SendEmailAsync(string email, string subject, string htmlMessage)
    {
        this.logger.LogInformation("Email sent to '{RecipientEmail}' with subject '{EmailSubject}' and message '{HtmlMessage}'.", email, subject, htmlMessage);

        return Task.CompletedTask;
    }
}