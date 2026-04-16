namespace LayeredTemplate.Auth.Web.Infrastructure.Sms.Services;

public interface ISmsSender
{
    Task SendAsync(string phoneNumber, string message);
}
