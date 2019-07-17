using UKSFWebsite.Api.Models.Accounts;

namespace UKSFWebsite.Api.Services.Abstraction {
    public interface ISessionService {
        Account GetContextAccount();
        string GetContextEmail();
        string GetContextId();
        bool ContextHasRole(string role);
    }
}
