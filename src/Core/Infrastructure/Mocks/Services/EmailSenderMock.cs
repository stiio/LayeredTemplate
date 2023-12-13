using System.Diagnostics;
using LayeredTemplate.Application.Common.Services;

namespace LayeredTemplate.Infrastructure.Mocks.Services;

internal class EmailSenderMock : IEmailSender
{
    public Task SendEmail(string to, string subject, string htmlBody)
    {
        return this.SendEmail(new[] { to }, subject, htmlBody);
    }

    public Task SendEmail(string[] tos, string subject, string htmlBody)
    {
        var emails = string.Join(", ", tos);
        Debug.WriteLine($"Emails: {emails}\nSubject: {subject}\nMessage:\n{htmlBody}");
        return Task.CompletedTask;
    }
}