using UKSF.Api.Core;
using UKSF.Api.Core.Extensions;
using UKSF.Api.Core.Models;
using UKSF.Api.Core.Services;

namespace UKSF.Api.Services;

public interface IDocumentPermissionsService
{
    bool DoesContextHaveReadPermission(DomainMetadataWithPermissions metadataWithPermissions);
    bool DoesContextHaveWritePermission(DomainMetadataWithPermissions metadataWithPermissions);
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

    public bool DoesContextHaveReadPermission(DomainMetadataWithPermissions metadataWithPermissions)
    {
        return DoesContextHavePermission(metadataWithPermissions, false);
    }

    public bool DoesContextHaveWritePermission(DomainMetadataWithPermissions metadataWithPermissions)
    {
        return DoesContextHavePermission(metadataWithPermissions, true);
    }

    private bool DoesContextHavePermission(DomainMetadataWithPermissions metadataWithPermissions, bool write)
    {
        if (_httpContextService.UserHasPermission(Permissions.Superadmin))
        {
            return true;
        }

        var id = _httpContextService.GetUserId();
        var documentPermissions = write ? metadataWithPermissions.WritePermissions : metadataWithPermissions.ReadPermissions;

        var hasPermission = true;
        if (documentPermissions.Units.Any())
        {
            hasPermission = ValidateUnitPermissions(documentPermissions, id, write);
        }

        if (!documentPermissions.Rank.IsNullOrEmpty())
        {
            hasPermission &= ValidateRankPermissions(documentPermissions.Rank);
        }

        return hasPermission;
    }

    private bool ValidateUnitPermissions(DocumentPermissions permissions, string memberId, bool write)
    {
        return permissions.SelectedUnitsOnly
            ? permissions.Units.Any(unitId => _unitsService.HasMember(unitId, memberId))
            : permissions.Units.Any(unitId => write ? _unitsService.AnyParentHasMember(unitId, memberId) : _unitsService.AnyChildHasMember(unitId, memberId));
    }

    private bool ValidateRankPermissions(string requiredRank)
    {
        var domainAccount = _accountService.GetUserAccount();
        var memberRank = domainAccount.Rank;
        return _ranksService.IsSuperiorOrEqual(memberRank, requiredRank);
    }
}
