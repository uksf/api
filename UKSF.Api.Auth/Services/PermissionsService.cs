using System.Collections.Generic;
using System.Linq;
using UKSF.Api.Admin.Extensions;
using UKSF.Api.Admin.Services;
using UKSF.Api.Personnel.Context;
using UKSF.Api.Personnel.Models;
using UKSF.Api.Personnel.Services;
using UKSF.Api.Shared;

namespace UKSF.Api.Auth.Services
{
    public interface IPermissionsService
    {
        IEnumerable<string> GrantPermissions(DomainAccount domainAccount);
    }

    public class PermissionsService : IPermissionsService
    {
        private readonly IRanksService _ranksService;
        private readonly IRecruitmentService _recruitmentService;
        private readonly IUnitsContext _unitsContext;
        private readonly IUnitsService _unitsService;
        private readonly IVariablesService _variablesService;

        public PermissionsService(IRanksService ranksService, IUnitsContext unitsContext, IUnitsService unitsService, IRecruitmentService recruitmentService, IVariablesService variablesService)
        {
            _ranksService = ranksService;
            _unitsContext = unitsContext;
            _unitsService = unitsService;
            _recruitmentService = recruitmentService;
            _variablesService = variablesService;
        }

        public IEnumerable<string> GrantPermissions(DomainAccount domainAccount)
        {
            HashSet<string> permissions = new();

            switch (domainAccount.MembershipState)
            {
                case MembershipState.MEMBER:
                {
                    permissions.Add(Permissions.MEMBER);
                    var admin = domainAccount.Admin;
                    if (admin)
                    {
                        permissions.UnionWith(Permissions.ALL);
                        break;
                    }

                    if (_unitsService.MemberHasAnyRole(domainAccount.Id))
                    {
                        permissions.Add(Permissions.COMMAND);
                    }

                    var ncoRank = _variablesService.GetVariable("PERMISSIONS_NCO_RANK").AsString();
                    if (domainAccount.Rank != null && _ranksService.IsSuperiorOrEqual(domainAccount.Rank, ncoRank))
                    {
                        permissions.Add(Permissions.NCO);
                    }

                    if (_recruitmentService.IsRecruiterLead(domainAccount))
                    {
                        permissions.Add(Permissions.RECRUITER_LEAD);
                    }

                    if (_recruitmentService.IsRecruiter(domainAccount))
                    {
                        permissions.Add(Permissions.RECRUITER);
                    }

                    var personnelId = _variablesService.GetVariable("UNIT_ID_PERSONNEL").AsString();
                    if (_unitsContext.GetSingle(personnelId).Members.Contains(domainAccount.Id))
                    {
                        permissions.Add(Permissions.PERSONNEL);
                    }

                    var missionsId = _variablesService.GetVariable("UNIT_ID_MISSIONS").AsArray();
                    if (_unitsContext.GetSingle(x => missionsId.Contains(x.Id)).Members.Contains(domainAccount.Id))
                    {
                        permissions.Add(Permissions.SERVERS);
                    }

                    var testersId = _variablesService.GetVariable("UNIT_ID_TESTERS").AsString();
                    if (_unitsContext.GetSingle(testersId).Members.Contains(domainAccount.Id))
                    {
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
