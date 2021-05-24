using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace UKSF.Api.Shared.Context
{
    public interface ISmtpClientContext
    {
        Task SendEmailAsync(MailMessage mailMessage);
    }

    public class SmtpClientContext : ISmtpClientContext
    {
        private readonly string _password;
        private readonly string _username;

        public SmtpClientContext(IConfiguration configuration)
        {
            _username = configuration.GetSection("EmailSettings")["username"];
            _password = configuration.GetSection("EmailSettings")["password"];
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
}
