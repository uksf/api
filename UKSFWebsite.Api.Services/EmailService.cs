using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;
using UKSFWebsite.Api.Services.Abstraction;

namespace UKSFWebsite.Api.Services {
    public class EmailService : IEmailService {
        private readonly string username;
        private readonly string password;

        public EmailService(IConfiguration configuration) {
            username = configuration.GetSection("EmailSettings")["username"];
            password = configuration.GetSection("EmailSettings")["password"];
        }

        public void SendEmail(string targetEmail, string subject, string htmlEmail) {
            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password)) return;
            using MailMessage mail = new MailMessage {From = new MailAddress(username, "UKSF")};
            mail.To.Add(targetEmail);
            mail.Subject = subject;
            mail.Body = htmlEmail;
            mail.IsBodyHtml = true;

            using SmtpClient smtp = new SmtpClient("smtp.gmail.com", 587) {Credentials = new NetworkCredential(username, password), EnableSsl = true};
            smtp.Send(mail);
        }
    }
}
