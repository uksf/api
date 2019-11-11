using System.Collections.Generic;
using UKSFWebsite.Api.Models.Personnel;

namespace UKSFWebsite.Api.Interfaces.Integrations {
    public interface ITeamspeakGroupService {
        void UpdateAccountGroups(Account account, ICollection<string> serverGroups, string clientDbId);
    }
}
