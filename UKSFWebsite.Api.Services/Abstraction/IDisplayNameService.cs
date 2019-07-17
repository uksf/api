using UKSFWebsite.Api.Models.Accounts;

namespace UKSFWebsite.Api.Services.Abstraction {
    public interface IDisplayNameService {
        string GetDisplayName(Account account);
        string GetDisplayName(string id);
        string GetDisplayNameWithoutRank(Account account);
    }
}
