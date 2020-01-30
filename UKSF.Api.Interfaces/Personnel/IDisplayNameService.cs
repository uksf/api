using UKSF.Api.Models.Personnel;

namespace UKSF.Api.Interfaces.Personnel {
    public interface IDisplayNameService {
        string GetDisplayName(Account account);
        string GetDisplayName(string id);
        string GetDisplayNameWithoutRank(Account account);
    }
}
