using UKSF.Api.Core;
using UKSF.Api.Core.Extensions;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Core.Services;

namespace UKSF.Api.Services;

public interface IDocumentPermissionsService
{
    bool DoesContextHaveReadPermission(DomainMetadataWithPermissions metadataWithPermissions);
    bool DoesContextHaveWritePermission(DomainMetadataWithPermissions metadataWithPermissions);
}

public class DocumentPermissionsService(
    IHttpContextService httpContextService,
    IUnitsService unitsService,
    IRanksService ranksService,
    IAccountService accountService
) : IDocumentPermissionsService
{
    public bool DoesContextHaveReadPermission(DomainMetadataWithPermissions metadataWithPermissions)
    {
        if (httpContextService.UserHasPermission(Permissions.Superadmin))
        {
            return true;
        }

        var memberId = httpContextService.GetUserId();
        return ValidateWritePermission(metadataWithPermissions.WritePermissions, memberId) ||
               ValidateReadPermission(metadataWithPermissions.ReadPermissions, memberId);
    }

    public bool DoesContextHaveWritePermission(DomainMetadataWithPermissions metadataWithPermissions)
    {
        if (httpContextService.UserHasPermission(Permissions.Superadmin))
        {
            return true;
        }

        var memberId = httpContextService.GetUserId();
        return ValidateWritePermission(metadataWithPermissions.WritePermissions, memberId);
    }

    private bool ValidateWritePermission(DocumentPermissions writePermissions, string memberId)
    {
        var checkUnitsPermission = writePermissions.Units.Any();
        var checkRankPermission = !writePermissions.Rank.IsNullOrEmpty();
        if (checkUnitsPermission && checkRankPermission)
        {
            return ValidateUnitWritePermissions(writePermissions, memberId) && ValidateRankPermissions(writePermissions);
        }

        if (checkUnitsPermission)
        {
            return ValidateUnitWritePermissions(writePermissions, memberId);
        }

        if (checkRankPermission)
        {
            return ValidateRankPermissions(writePermissions);
        }

        return false;
    }

    private bool ValidateReadPermission(DocumentPermissions readPermissions, string memberId)
    {
        var hasPermission = true;
        if (readPermissions.Units.Any())
        {
            hasPermission = ValidateUnitReadPermissions(readPermissions, memberId);
        }

        if (!readPermissions.Rank.IsNullOrEmpty())
        {
            hasPermission &= ValidateRankPermissions(readPermissions);
        }

        return hasPermission;
    }

    private bool ValidateUnitWritePermissions(DocumentPermissions writePermissions, string memberId)
    {
        return writePermissions.SelectedUnitsOnly
            ? writePermissions.Units.Any(unitId => unitsService.HasMember(unitId, memberId))
            : writePermissions.Units.Any(unitId => unitsService.AnyParentHasMember(unitId, memberId));
    }

    private bool ValidateUnitReadPermissions(DocumentPermissions readPermissions, string memberId)
    {
        return readPermissions.SelectedUnitsOnly
            ? readPermissions.Units.Any(unitId => unitsService.HasMember(unitId, memberId))
            : readPermissions.Units.Any(unitId => unitsService.AnyChildHasMember(unitId, memberId));
    }

    private bool ValidateRankPermissions(DocumentPermissions permissions)
    {
        var account = accountService.GetUserAccount();
        var memberRank = account.Rank;
        return ranksService.IsSuperiorOrEqual(memberRank, permissions.Rank);
    }
}
