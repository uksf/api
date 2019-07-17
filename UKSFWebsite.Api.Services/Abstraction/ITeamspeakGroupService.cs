using System.Collections.Generic;
using UKSFWebsite.Api.Models.Accounts;

namespace UKSFWebsite.Api.Services.Abstraction {
    public interface ITeamspeakGroupService {
        void UpdateAccountGroups(Account account, ICollection<string> serverGroups, string clientDbId);
    }
}
