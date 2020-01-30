using System.Collections.Generic;
using System.Threading.Tasks;
using UKSF.Api.Models.Personnel;

namespace UKSF.Api.Interfaces.Integrations.Teamspeak {
    public interface ITeamspeakGroupService {
        Task UpdateAccountGroups(Account account, ICollection<double> serverGroups, double clientDbId);
    }
}
