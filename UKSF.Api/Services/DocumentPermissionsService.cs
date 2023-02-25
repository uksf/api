using UKSF.Api.Core;
using UKSF.Api.Core.Extensions;
using UKSF.Api.Core.Models;
using UKSF.Api.Core.Services;

namespace UKSF.Api.Services;

public interface IDocumentPermissionsService
{
    bool DoesContextHaveReadPermission(DocumentPermissions documentPermissions);
    bool DoesContextHaveWritePermission(DocumentPermissions documentPermissions);
}

public class DocumentPermissionsService : IDocumentPermissionsService
{
    private readonly IAccountService _accountService;
    private readonly IHttpContextService _httpContextService;
    private readonly IRanksService _ranksService;
    private readonly IUnitsService _unitsService;

    public DocumentPermissionsService(
        IHttpContextService httpContextService,
        IUnitsService unitsService,
        IRanksService ranksService,
        IAccountService accountService
    )
    {
        _httpContextService = httpContextService;
        _unitsService = unitsService;
        _ranksService = ranksService;
        _accountService = accountService;
    }

    public bool DoesContextHaveReadPermission(DocumentPermissions documentPermissions)
    {
        return DoesContextHavePermission(documentPermissions, false);
    }

    public bool DoesContextHaveWritePermission(DocumentPermissions documentPermissions)
    {
        return DoesContextHavePermission(documentPermissions, true);
    }

    private bool DoesContextHavePermission(DocumentPermissions documentPermissions, bool checkUnitParents)
    {
        if (_httpContextService.UserHasPermission(Permissions.Superadmin))
        {
            return true;
        }

        var id = _httpContextService.GetUserId();

        var hasPermission = true;
        if (documentPermissions.Units.Any())
        {
            hasPermission = ValidateUnitPermissions(documentPermissions.Units, id, checkUnitParents);
        }

        if (!documentPermissions.Rank.IsNullOrEmpty())
        {
            hasPermission &= ValidateRankPermissions(documentPermissions.Rank);
        }

        return hasPermission;
    }

    private bool ValidateUnitPermissions(IReadOnlyCollection<string> unitIds, string memberId, bool checkUnitParents)
    {
        return unitIds.Any(unitId => _unitsService.AnyParentHasMember(unitId, memberId));
        // var writePermission = unitIds.Any(unitId => _unitsService.AnyParentHasMember(unitId, memberId));
        // if (checkUnitParents)
        // {
        //     return writePermission;
        // }
        //
        // return writePermission || unitIds.Any(unitId => _unitsService.AnyChildHasMember(unitId, memberId));
    }

    private bool ValidateRankPermissions(string requiredRank)
    {
        var domainAccount = _accountService.GetUserAccount();
        var memberRank = domainAccount.Rank;
        return _ranksService.IsSuperiorOrEqual(memberRank, requiredRank);
    }
}
