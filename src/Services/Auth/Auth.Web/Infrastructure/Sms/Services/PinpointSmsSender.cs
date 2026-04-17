namespace LayeredTemplate.Auth.Web.Infrastructure.Sms.Services;

public class PinpointSmsSender : ISmsSender
{
    public Task SendAsync(string phoneNumber, string message)
    {
        throw new NotImplementedException();
    }
}