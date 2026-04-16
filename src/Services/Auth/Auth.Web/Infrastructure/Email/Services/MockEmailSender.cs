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
        using var scope = this.logger.BeginScope(new Dictionary<string, object>()
        {
            ["RecipientEmail"] = email,
            ["EmailSubject"] = subject,
            ["EmailBody"] = htmlMessage,
            ["IsMockEmailSender"] = true,
        });

        this.logger.LogInformation("Email sent.");

        return Task.CompletedTask;
    }
}