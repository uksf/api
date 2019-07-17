namespace UKSFWebsite.Api.Services.Abstraction {
    public interface IEmailService {
        void SendEmail(string targetEmail, string subject, string htmlEmail);
    }
}