using System.Collections.Generic;
using UKSFWebsite.Api.Models.Personnel;

namespace UKSFWebsite.Api.Interfaces.Integrations.Teamspeak {
    public interface ITeamspeakGroupService {
        void UpdateAccountGroups(Account account, ICollection<string> serverGroups, string clientDbId);
    }
}
