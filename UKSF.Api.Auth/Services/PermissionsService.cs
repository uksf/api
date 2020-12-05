using System.Collections.Generic;
using System.Linq;
using UKSF.Api.Admin.Extensions;
using UKSF.Api.Admin.Services;
using UKSF.Api.Personnel.Context;
using UKSF.Api.Personnel.Models;
using UKSF.Api.Personnel.Services;
using UKSF.Api.Shared;

namespace UKSF.Api.Auth.Services {
    public interface IPermissionsService {
        IEnumerable<string> GrantPermissions(Account account);
    }

    public class PermissionsService : IPermissionsService {
        private readonly IRanksService _ranksService;
        private readonly IRecruitmentService _recruitmentService;
        private readonly IUnitsContext _unitsContext;
        private readonly IUnitsService _unitsService;
        private readonly IVariablesService _variablesService;

        public PermissionsService(IRanksService ranksService, IUnitsContext unitsContext, IUnitsService unitsService, IRecruitmentService recruitmentService, IVariablesService variablesService) {
            _ranksService = ranksService;
            _unitsContext = unitsContext;
            _unitsService = unitsService;
            _recruitmentService = recruitmentService;
            _variablesService = variablesService;
        }

        public IEnumerable<string> GrantPermissions(Account account) {
            HashSet<string> permissions = new();

            switch (account.MembershipState) {
                case MembershipState.MEMBER: {
                    permissions.Add(Permissions.MEMBER);
                    bool admin = account.Admin;
                    if (admin) {
                        permissions.UnionWith(Permissions.ALL);
                        break;
                    }

                    if (_unitsService.MemberHasAnyRole(account.Id)) {
                        permissions.Add(Permissions.COMMAND);
                    }

                    string ncoRank = _variablesService.GetVariable("PERMISSIONS_NCO_RANK").AsString();
                    if (account.Rank != null && _ranksService.IsSuperiorOrEqual(account.Rank, ncoRank)) {
                        permissions.Add(Permissions.NCO);
                    }

                    if (_recruitmentService.IsRecruiterLead(account)) {
                        permissions.Add(Permissions.RECRUITER_LEAD);
                    }

                    if (_recruitmentService.IsRecruiter(account)) {
                        permissions.Add(Permissions.RECRUITER);
                    }

                    string personnelId = _variablesService.GetVariable("UNIT_ID_PERSONNEL").AsString();
                    if (_unitsContext.GetSingle(personnelId).Members.Contains(account.Id)) {
                        permissions.Add(Permissions.PERSONNEL);
                    }

                    string[] missionsId = _variablesService.GetVariable("UNIT_ID_MISSIONS").AsArray();
                    if (_unitsContext.GetSingle(x => missionsId.Contains(x.Id)).Members.Contains(account.Id)) {
                        permissions.Add(Permissions.SERVERS);
                    }

                    string testersId = _variablesService.GetVariable("UNIT_ID_TESTERS").AsString();
                    if (_unitsContext.GetSingle(testersId).Members.Contains(account.Id)) {
                        permissions.Add(Permissions.TESTER);
                    }

                    break;
                }

                case MembershipState.SERVER:
                    permissions.Add(Permissions.ADMIN);
                    break;
                case MembershipState.CONFIRMED:
                    permissions.Add(Permissions.CONFIRMED);
                    break;
                case MembershipState.DISCHARGED:
                    permissions.Add(Permissions.DISCHARGED);
                    break;
                default:
                    permissions.Add(Permissions.UNCONFIRMED);
                    break;
            }

            return permissions;
        }
    }
}
