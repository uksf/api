using UKSF.Api.Models.Personnel;

namespace UKSF.Api.Interfaces.Utility {
    public interface ISessionService {
        Account GetContextAccount();
        string GetContextEmail();
        string GetContextId();
        bool ContextHasRole(string role);
    }
}
