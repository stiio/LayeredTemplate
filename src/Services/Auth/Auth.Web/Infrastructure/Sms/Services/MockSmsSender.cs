namespace LayeredTemplate.Auth.Web.Infrastructure.Sms.Services;

public class MockSmsSender : ISmsSender
{
    private readonly ILogger<MockSmsSender> logger;

    public MockSmsSender(ILogger<MockSmsSender> logger)
    {
        this.logger = logger;
    }

    public Task SendAsync(string phoneNumber, string message)
    {
        this.logger.LogInformation("SMS sent to {Phone}: {Message}", phoneNumber, message);
        return Task.CompletedTask;
    }
}
