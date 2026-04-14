namespace LayeredTemplate.Auth.Web.Infrastructure.Sms;

public interface ISmsSender
{
    Task SendAsync(string phoneNumber, string message);
}
