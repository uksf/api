using UKSFWebsite.Api.Models.Personnel;

namespace UKSFWebsite.Api.Interfaces.Utility {
    public interface ISessionService {
        Account GetContextAccount();
        string GetContextEmail();
        string GetContextId();
        bool ContextHasRole(string role);
    }
}
