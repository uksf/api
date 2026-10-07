using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;
using UKSF.Api.Core.Configuration;
using UKSF.Api.Core.Services;

namespace UKSF.Api.Core.Context;

public interface ISmtpClientContext
{
    Task SendEmailAsync(MailMessage mailMessage);
}

public class SmtpClientContext : ISmtpClientContext
{
    private const string VerifySender = "verify@uksf.local";

    private readonly string _password;
    private readonly string _username;
    private readonly VerifyMode _verifyMode;

    public SmtpClientContext(IOptions<AppSettings> options, VerifyMode verifyMode)
    {
        var appSettings = options.Value;
        _username = appSettings.Secrets.Email.Username;
        _password = appSettings.Secrets.Email.Password;
        _verifyMode = verifyMode;
    }

    public async Task SendEmailAsync(MailMessage mailMessage)
    {
        if (_verifyMode.Enabled)
        {
            await WriteToEmailDirectory(mailMessage);
            return;
        }

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

    private async Task WriteToEmailDirectory(MailMessage mailMessage)
    {
        Directory.CreateDirectory(_verifyMode.EmailDirectory);
        mailMessage.From = new MailAddress(string.IsNullOrEmpty(_username) ? VerifySender : _username, "UKSF");

        using SmtpClient pickup = new();
        pickup.DeliveryMethod = SmtpDeliveryMethod.SpecifiedPickupDirectory;
        pickup.PickupDirectoryLocation = _verifyMode.EmailDirectory;
        await pickup.SendMailAsync(mailMessage);
    }
}
