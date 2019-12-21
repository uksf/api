using System.Collections.Generic;
using UKSFWebsite.Api.Models.Documents;

namespace UKSFWebsite.Api.Services.Abstraction {
    public interface IDocumentPermissionService {
        bool HasPermission(DocumentPermissions permissions, string id);
        void UpdateRankPermissions(DocumentPermissions permissions, string rank);
        void UpdateUnitPermissions(DocumentPermissions permissions, IEnumerable<string> units);
        void UpdateTrainingPermissions(DocumentPermissions permissions, IEnumerable<string> trainings);
        void UpdateUserPermissions(DocumentPermissions permissions, IEnumerable<string> users);
    }
}
