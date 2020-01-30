namespace UKSF.Api.Interfaces.Utility {
    public interface ILoginService {
        string Login(string email, string password);
        string LoginWithoutPassword(string email);
        string RegenerateToken(string accountId);
    }
}
