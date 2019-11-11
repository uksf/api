using System.Threading.Tasks;
using UKSFWebsite.Api.Models.Message;

namespace UKSFWebsite.Api.Interfaces.Personnel {
    public interface IAssignmentService {
        Task AssignUnitRole(string id, string unitId, string role);
        Task UnassignAllUnits(string id);
        Task UnassignAllUnitRoles(string id);
        Task<Notification> UpdateUnitRankAndRole(string id, string unitString = "", string role = "", string rankString = "", string notes = "", string message = "", string reason = "");
        Task<string> UnassignUnitRole(string id, string unitId);
        Task UnassignUnit(string id, string unitId);
    }
}
