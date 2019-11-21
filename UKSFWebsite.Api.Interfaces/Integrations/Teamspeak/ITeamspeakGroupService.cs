using System.Collections.Generic;
using System.Threading.Tasks;
using UKSFWebsite.Api.Models.Personnel;

namespace UKSFWebsite.Api.Interfaces.Integrations.Teamspeak {
    public interface ITeamspeakGroupService {
        Task UpdateAccountGroups(Account account, ICollection<double> serverGroups, double clientDbId);
    }
}
