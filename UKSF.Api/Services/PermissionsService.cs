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

public class PermissionsService : IPermissionsService
{
    private readonly IRanksService _ranksService;
    private readonly IRecruitmentService _recruitmentService;
    private readonly IUnitsContext _unitsContext;
    private readonly IUnitsService _unitsService;
    private readonly IVariablesService _variablesService;

    public PermissionsService(
        IRanksService ranksService,
        IUnitsContext unitsContext,
        IUnitsService unitsService,
        IRecruitmentService recruitmentService,
        IVariablesService variablesService
    )
    {
        _ranksService = ranksService;
        _unitsContext = unitsContext;
        _unitsService = unitsService;
        _recruitmentService = recruitmentService;
        _variablesService = variablesService;
    }

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

                if (_unitsService.MemberHasAnyRole(account.Id))
                {
                    permissions.Add(Permissions.Command);
                }

                var ncoRank = _variablesService.GetVariable("PERMISSIONS_NCO_RANK").AsString();
                if (account.Rank is not null && _ranksService.IsSuperiorOrEqual(account.Rank, ncoRank))
                {
                    permissions.Add(Permissions.Nco);
                }

                if (_recruitmentService.IsRecruiterLead(account))
                {
                    permissions.Add(Permissions.RecruiterLead);
                }

                if (_recruitmentService.IsRecruiter(account))
                {
                    permissions.Add(Permissions.Recruiter);
                }

                var personnelId = _variablesService.GetVariable("UNIT_ID_PERSONNEL").AsString();
                if (_unitsContext.GetSingle(personnelId).Members.Contains(account.Id))
                {
                    permissions.Add(Permissions.Personnel);
                }

                var missionsId = _variablesService.GetVariable("UNIT_ID_MISSIONS").AsArray();
                if (_unitsContext.Get(x => missionsId.Contains(x.Id)).Any(x => x.Members.Contains(account.Id)))
                {
                    permissions.Add(Permissions.Servers);
                }

                var testersId = _variablesService.GetVariable("UNIT_ID_TESTERS").AsString();
                if (_unitsContext.GetSingle(testersId).Members.Contains(account.Id))
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
