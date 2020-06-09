using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UKSF.Api.Models.Personnel;

namespace UKSF.Api.Interfaces.Personnel {
    public interface IRecruitmentService {
        object GetAllApplications();
        JObject GetApplication(Account account);
        object GetActiveRecruiters();
        IEnumerable<Account> GetRecruiters(bool skipSort = false);
        Dictionary<string, string> GetRecruiterLeads();
        object GetStats(string account, bool monthly);
        string GetRecruiter();
        bool IsRecruiterLead(Account account = null);
        bool IsRecruiter(Account account);
        Task SetRecruiter(string id, string newRecruiter);
    }
}
