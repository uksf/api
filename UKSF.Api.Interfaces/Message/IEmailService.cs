namespace UKSF.Api.Interfaces.Message {
    public interface IEmailService {
        void SendEmail(string targetEmail, string subject, string htmlEmail);
    }
}
