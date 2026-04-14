namespace LayeredTemplate.Auth.Web.Services;

public interface ISmsSender
{
    Task SendAsync(string phoneNumber, string message);
}
