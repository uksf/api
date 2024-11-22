using AngleSharp.Text;
using UKSF.Api.Core;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Extensions;
using UKSF.Api.Core.Models;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Core.Services;

namespace UKSF.Api.Services;

public interface IPermissionsService
{
    IEnumerable<string> GrantPermissions(DomainAccount account);
}

public class PermissionsService(
    IRanksService ranksService,
    IUnitsContext unitsContext,
    IUnitsService unitsService,
    IRecruitmentService recruitmentService,
    IVariablesService variablesService
) : IPermissionsService
{
    public IEnumerable<string> GrantPermissions(DomainAccount account)
    {
        HashSet<string> permissions = new();

        switch (account.MembershipState)
        {
            case MembershipState.Member:
            {
                permissions.Add(Permissions.Member);

                if (account.Admin)
                {
                    permissions.UnionWith(Permissions.All);

                    if (account.SuperAdmin)
                    {
                        permissions.Add(Permissions.Superadmin);
                    }

                    break;
                }

                if (unitsService.MemberHasAnyRole(account.Id))
                {
                    permissions.Add(Permissions.Command);
                }

                var ncoRank = variablesService.GetVariable("PERMISSIONS_NCO_RANK").AsString();
                if (account.Rank is not null && ranksService.IsSuperiorOrEqual(account.Rank, ncoRank))
                {
                    permissions.Add(Permissions.Nco);
                }

                if (recruitmentService.IsRecruiterLead(account))
                {
                    permissions.Add(Permissions.RecruiterLead);
                }

                if (recruitmentService.IsRecruiter(account))
                {
                    permissions.Add(Permissions.Recruiter);
                }

                var personnelId = variablesService.GetVariable("UNIT_ID_PERSONNEL").AsString();
                if (unitsContext.GetSingle(personnelId).Members.Contains(account.Id))
                {
                    permissions.Add(Permissions.Personnel);
                }

                var missionsId = variablesService.GetVariable("UNIT_ID_MISSIONS").AsArray();
                if (unitsContext.Get(x => missionsId.Contains(x.Id)).Any(x => x.Members.Contains(account.Id)))
                {
                    permissions.Add(Permissions.Servers);
                }

                var testersId = variablesService.GetVariable("UNIT_ID_TESTERS").AsString();
                if (unitsContext.GetSingle(testersId).Members.Contains(account.Id))
                {
                    permissions.Add(Permissions.Tester);
                }

                break;
            }

            case MembershipState.Server:
                permissions.Add(Permissions.Admin);
                break;
            case MembershipState.Confirmed:
                permissions.Add(Permissions.Confirmed);
                break;
            case MembershipState.Discharged:
                permissions.Add(Permissions.Discharged);
                break;
            default:
                permissions.Add(Permissions.Unconfirmed);
                break;
        }

        return permissions;
    }
}
