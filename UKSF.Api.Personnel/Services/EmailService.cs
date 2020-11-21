using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;

namespace UKSF.Api.Personnel.Services {
    public interface IEmailService {
        void SendEmail(string targetEmail, string subject, string htmlEmail);
    }

    public class EmailService : IEmailService {
        private readonly string _password;
        private readonly string _username;

        public EmailService(IConfiguration configuration) {
            _username = configuration.GetSection("EmailSettings")["username"];
            _password = configuration.GetSection("EmailSettings")["password"];
        }

        public void SendEmail(string targetEmail, string subject, string htmlEmail) {
            if (string.IsNullOrEmpty(_username) || string.IsNullOrEmpty(_password)) return;
            using MailMessage mail = new() { From = new MailAddress(_username, "UKSF") };
            mail.To.Add(targetEmail);
            mail.Subject = subject;
            mail.Body = htmlEmail;
            mail.IsBodyHtml = true;

            using SmtpClient smtp = new("smtp.gmail.com", 587) { Credentials = new NetworkCredential(_username, _password), EnableSsl = true };
            smtp.Send(mail);
        }
    }
}
