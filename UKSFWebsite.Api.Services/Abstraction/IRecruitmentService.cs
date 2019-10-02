using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UKSFWebsite.Api.Models.Accounts;

namespace UKSFWebsite.Api.Services.Abstraction {
    public interface IRecruitmentService {
        object GetAllApplications();
        JObject GetApplication(Account account);
        object GetActiveRecruiters();
        IEnumerable<Account> GetSr1Members(bool skipSort = false);
        Dictionary<string, string> GetSr1Leads();
        object GetStats(string account, bool monthly);
        string GetRecruiter();
        bool IsAccountSr1Lead(Account account = null);
        bool IsRecruiter(Account account);
        Task SetRecruiter(string id, string newRecruiter);
    }
}
