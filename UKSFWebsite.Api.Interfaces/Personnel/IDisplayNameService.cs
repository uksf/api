using UKSFWebsite.Api.Models.Personnel;

namespace UKSFWebsite.Api.Interfaces.Personnel {
    public interface IDisplayNameService {
        string GetDisplayName(Account account);
        string GetDisplayName(string id);
        string GetDisplayNameWithoutRank(Account account);
    }
}
