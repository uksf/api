using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using UKSF.Api.Base.Configuration;

namespace UKSF.Api.Shared.Context;

public interface ISmtpClientContext
{
    Task SendEmailAsync(MailMessage mailMessage);
}

public class SmtpClientContext : ISmtpClientContext
{
    private readonly string _password;
    private readonly string _username;

    public SmtpClientContext(IOptions<AppSettings> options)
    {
        var appSettings = options.Value;
        _username = appSettings.Secrets.Email.Username;
        _password = appSettings.Secrets.Email.Password;
    }

    public async Task SendEmailAsync(MailMessage mailMessage)
    {
        if (string.IsNullOrEmpty(_username) || string.IsNullOrEmpty(_password))
        {
            return;
        }

        mailMessage.From = new(_username, "UKSF");

        using SmtpClient smtp = new("smtp.gmail.com", 587) { Credentials = new NetworkCredential(_username, _password), EnableSsl = true };
        await smtp.SendMailAsync(mailMessage);
    }
}
