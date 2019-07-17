namespace UKSFWebsite.Api.Services.Abstraction {
    public interface ILoginService {
        string Login(string email, string password);
        string LoginWithoutPassword(string email);
        string RegenerateToken(string accountId);
    }
}
