using System.Threading.Tasks;

namespace UKSFWebsite.Api.Services.Abstraction {
    public interface IAssignmentService {
        Task AssignUnitRole(string id, string unitId, string role);
        Task UnassignAllUnits(string id);
        Task UnassignAllUnitRoles(string id);
        Task UpdateUnitRankAndRole(string id, string unitString = "", string role = "", string rankString = "", string notes = "", string message = "", string reason = "");
        Task UnassignUnitRole(string id, string unitId);
        Task UnassignUnit(string id, string unitId);
    }
}
