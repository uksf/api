using System.Collections.Generic;
using System.Linq;
using UKSF.Api.Interfaces.Admin;
using UKSF.Api.Interfaces.Personnel;
using UKSF.Api.Interfaces.Units;
using UKSF.Api.Models.Personnel;
using UKSF.Common;

namespace UKSF.Api.Accounts.Services.Auth {
    public interface IPermissionsService {
        IEnumerable<string> GrantPermissions(Account account);
    }

    public class PermissionsService : IPermissionsService {
        private readonly string[] admins = { "59e38f10594c603b78aa9dbd", "5a1e894463d0f71710089106", "5a1ae0f0b9bcb113a44edada" }; // TODO: Make this an account flag
        private readonly IRanksService ranksService;
        private readonly IRecruitmentService recruitmentService;
        private readonly IUnitsService unitsService;
        private readonly IVariablesService variablesService;

        public PermissionsService(IRanksService ranksService, IUnitsService unitsService, IRecruitmentService recruitmentService, IVariablesService variablesService) {
            this.ranksService = ranksService;
            this.unitsService = unitsService;
            this.recruitmentService = recruitmentService;
            this.variablesService = variablesService;
        }

        public IEnumerable<string> GrantPermissions(Account account) {
            HashSet<string> permissions = new HashSet<string>();

            switch (account.membershipState) {
                case MembershipState.MEMBER: {
                    permissions.Add(Permissions.MEMBER);
                    bool admin = admins.Contains(account.id);
                    if (admin) {
                        permissions.UnionWith(Permissions.ALL);
                        break;
                    }

                    if (unitsService.MemberHasAnyRole(account.id)) {
                        permissions.Add(Permissions.COMMAND);
                    }

                    // TODO: Remove hardcoded value
                    if (account.rank != null && ranksService.IsSuperiorOrEqual(account.rank, "Senior Aircraftman")) {
                        permissions.Add(Permissions.NCO);
                    }

                    if (recruitmentService.IsRecruiterLead(account)) {
                        permissions.Add(Permissions.RECRUITER_LEAD);
                    }

                    if (recruitmentService.IsRecruiter(account)) {
                        permissions.Add(Permissions.RECRUITER);
                    }

                    string personnelId = variablesService.GetVariable("UNIT_ID_PERSONNEL").AsString();
                    if (unitsService.Data.GetSingle(personnelId).members.Contains(account.id)) {
                        permissions.Add(Permissions.PERSONNEL);
                    }

                    string[] missionsId = variablesService.GetVariable("UNIT_ID_MISSIONS").AsArray();
                    if (unitsService.Data.GetSingle(x => missionsId.Contains(x.id)).members.Contains(account.id)) {
                        permissions.Add(Permissions.SERVERS);
                    }

                    string testersId = variablesService.GetVariable("UNIT_ID_TESTERS").AsString();
                    if (unitsService.Data.GetSingle(testersId).members.Contains(account.id)) {
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
