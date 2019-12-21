using System.Collections.Generic;
using System.Linq;
using UKSFWebsite.Api.Interfaces.Personnel;
using UKSFWebsite.Api.Interfaces.Units;
using UKSFWebsite.Api.Models.Documents;
using UKSFWebsite.Api.Models.Personnel;
using UKSFWebsite.Api.Services.Abstraction;

namespace UKSFWebsite.Api.Services.Documents {
    public class DocumentPermissionService : IDocumentPermissionService {
        private readonly IAccountService accountService;
        private readonly IRanksService ranksService;
        private readonly IUnitsService unitsService;

        public DocumentPermissionService(IAccountService accountService, IUnitsService unitsService, IRanksService ranksService) {
            this.accountService = accountService;
            this.unitsService = unitsService;
            this.ranksService = ranksService;
        }

        public bool HasPermission(DocumentPermissions permissions, string id) {
            if (permissions.permissionTypes.Contains(DocumentPermissionType.ANY)) return true;
            if (permissions.permissionTypes.Contains(DocumentPermissionType.USER) && permissions.allowedUsers.Contains(id)) return true;

            bool hasRank = HasRankPermission(permissions, id);
            bool hasUnit = HasUnitPermission(permissions, id);
            bool hasTraining = HasTrainingPermission(permissions, id);

            // TODO: Make some better permission builder
            return permissions.firstOr
                ? permissions.secondOr
                    ? hasRank || hasUnit || hasTraining
                    : hasRank || hasUnit && hasTraining
                : !permissions.secondOr
                    ? hasRank && hasUnit && hasTraining
                    : hasRank && hasUnit || hasTraining;
        }

        public void UpdateRankPermissions(DocumentPermissions permissions, string rank) {
            permissions.minRank = rank;
            if (!permissions.permissionTypes.Contains(DocumentPermissionType.RANK)) {
                permissions.permissionTypes.Add(DocumentPermissionType.RANK);
            }
        }

        public void UpdateUnitPermissions(DocumentPermissions permissions, IEnumerable<string> units) {
            permissions.allowedUnits.UnionWith(units);
            if (!permissions.permissionTypes.Contains(DocumentPermissionType.UNIT)) {
                permissions.permissionTypes.Add(DocumentPermissionType.UNIT);
            }
        }

        public void UpdateTrainingPermissions(DocumentPermissions permissions, IEnumerable<string> trainings) {
            permissions.allowedTrainings.UnionWith(trainings);
            if (!permissions.permissionTypes.Contains(DocumentPermissionType.TRAINING)) {
                permissions.permissionTypes.Add(DocumentPermissionType.TRAINING);
            }
        }

        public void UpdateUserPermissions(DocumentPermissions permissions, IEnumerable<string> users) {
            permissions.allowedUsers.UnionWith(users);
            if (!permissions.permissionTypes.Contains(DocumentPermissionType.USER)) {
                permissions.permissionTypes.Add(DocumentPermissionType.USER);
            }
        }

        private bool HasRankPermission(DocumentPermissions permissions, string id) {
            if (permissions.permissionTypes.Contains(DocumentPermissionType.RANK)) {
                Account account = accountService.Data().GetSingle(id);
                return ranksService.IsSuperiorOrEqual(account.rank, permissions.minRank);
            }

            return false;
        }

        private bool HasUnitPermission(DocumentPermissions permissions, string id) {
            return permissions.permissionTypes.Contains(DocumentPermissionType.UNIT) && permissions.allowedUnits.Any(unitId => unitsService.HasMember(unitId, id));
        }

        private bool HasTrainingPermission(DocumentPermissions permissions, string id) => true; // Not Implemented
    }
}
