using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;
using UKSF.Api.Core.Configuration;

namespace UKSF.Api.Core.Context;

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

        mailMessage.From = new MailAddress(_username, "UKSF");

        using SmtpClient smtp = new("smtp.gmail.com", 587);
        smtp.Credentials = new NetworkCredential(_username, _password);
        smtp.EnableSsl = true;
        await smtp.SendMailAsync(mailMessage);
    }
}
