using MongoDB.Bson;
using UKSF.Api.Core;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Extensions;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Core.Services;

namespace UKSF.Api.Services;

public interface IRoleBasedDocumentPermissionsService
{
    bool DoesContextHaveReadPermission(DomainMetadataWithPermissions metadataWithPermissions);
    bool DoesContextHaveWritePermission(DomainMetadataWithPermissions metadataWithPermissions);
    RoleBasedDocumentPermissions GetEffectivePermissions(DomainMetadataWithPermissions metadataWithPermissions);
    RoleBasedDocumentPermissions GetInheritedPermissions(DomainMetadataWithPermissions metadataWithPermissions);
}

public class RoleBasedDocumentPermissionsService(
    IHttpContextService httpContextService,
    IUnitsService unitsService,
    IRanksService ranksService,
    IAccountService accountService,
    IDocumentFolderMetadataContext documentFolderMetadataContext
) : IRoleBasedDocumentPermissionsService
{
    public bool DoesContextHaveReadPermission(DomainMetadataWithPermissions metadataWithPermissions)
    {
        if (httpContextService.UserHasPermission(Permissions.Superadmin))
        {
            return true;
        }

        var memberId = httpContextService.GetUserId();

        // Check ownership
        if (!string.IsNullOrEmpty(metadataWithPermissions.Owner) && metadataWithPermissions.Owner == memberId)
        {
            return true;
        }

        var effectivePermissions = GetEffectivePermissions(metadataWithPermissions);

        // Check collaborator role (has both read and write access)
        if (HasRolePermission(effectivePermissions.Collaborators, memberId, isCollaboratorRole: true))
        {
            return true;
        }

        // Check viewer role (read-only access)
        return HasRolePermission(effectivePermissions.Viewers, memberId, isCollaboratorRole: false);
    }

    public bool DoesContextHaveWritePermission(DomainMetadataWithPermissions metadataWithPermissions)
    {
        if (httpContextService.UserHasPermission(Permissions.Superadmin))
        {
            return true;
        }

        var memberId = httpContextService.GetUserId();

        // Check ownership
        if (!string.IsNullOrEmpty(metadataWithPermissions.Owner) && metadataWithPermissions.Owner == memberId)
        {
            return true;
        }

        var effectivePermissions = GetEffectivePermissions(metadataWithPermissions);

        // Only collaborators have write access
        return HasRolePermission(effectivePermissions.Collaborators, memberId, isCollaboratorRole: true);
    }

    public RoleBasedDocumentPermissions GetEffectivePermissions(DomainMetadataWithPermissions metadataWithPermissions)
    {
        var effectivePermissions = new RoleBasedDocumentPermissions();

        // Start with inherited permissions (always returns a valid object now)
        var inheritedPermissions = GetInheritedPermissions(metadataWithPermissions);
        effectivePermissions.Viewers = ClonePermissionRole(inheritedPermissions.Viewers);
        effectivePermissions.Collaborators = ClonePermissionRole(inheritedPermissions.Collaborators);

        // Override with local permissions where they exist
        var localPermissions = metadataWithPermissions.RoleBasedPermissions;
        if (localPermissions != null)
        {
            if (HasPermissionRoleData(localPermissions.Viewers))
            {
                effectivePermissions.Viewers = ClonePermissionRole(localPermissions.Viewers);
            }

            if (HasPermissionRoleData(localPermissions.Collaborators))
            {
                effectivePermissions.Collaborators = ClonePermissionRole(localPermissions.Collaborators);
            }
        }

        return effectivePermissions;
    }

    public RoleBasedDocumentPermissions GetInheritedPermissions(DomainMetadataWithPermissions metadataWithPermissions)
    {
        // Only folders can have parents, documents inherit from their folder
        if (metadataWithPermissions is DomainDocumentFolderMetadata folderMetadata)
        {
            return GetInheritedPermissionsFromParentHierarchy(folderMetadata.Parent);
        }

        if (metadataWithPermissions is DomainDocumentMetadata documentMetadata)
        {
            // For documents, inherit from the parent folder hierarchy
            return GetInheritedPermissionsFromParentHierarchy(documentMetadata.Folder);
        }

        // Return empty permissions for unknown types
        return new RoleBasedDocumentPermissions();
    }

    private RoleBasedDocumentPermissions GetInheritedPermissionsFromParentHierarchy(string parentId)
    {
        var viewerPermissions = GetInheritedRolePermissions(parentId, isViewerRole: true);
        var collaboratorPermissions = GetInheritedRolePermissions(parentId, isViewerRole: false);

        // Always return a permissions object to ensure consistent API responses
        return new RoleBasedDocumentPermissions
        {
            Viewers = viewerPermissions ?? new PermissionRole(), Collaborators = collaboratorPermissions ?? new PermissionRole()
        };
    }

    private PermissionRole GetInheritedRolePermissions(string parentId, bool isViewerRole)
    {
        // Base case: no parent or reached root
        if (string.IsNullOrEmpty(parentId) || parentId == ObjectId.Empty.ToString())
        {
            return null; // No inherited permissions found
        }

        var parentFolder = documentFolderMetadataContext.GetSingle(parentId);
        if (parentFolder == null)
        {
            return null; // Parent not found
        }

        // Check if this parent has the specific role permissions set
        var rolePermissions = isViewerRole ? parentFolder.RoleBasedPermissions?.Viewers : parentFolder.RoleBasedPermissions?.Collaborators;

        if (HasPermissionRoleData(rolePermissions))
        {
            return ClonePermissionRole(rolePermissions);
        }

        // Recursive case: continue up the hierarchy for this specific role
        return GetInheritedRolePermissions(parentFolder.Parent, isViewerRole);
    }

    private static bool HasLocalPermissions(DomainDocumentFolderMetadata folder)
    {
        var rbp = folder.RoleBasedPermissions;
        return rbp != null && (HasPermissionRoleData(rbp.Viewers) || HasPermissionRoleData(rbp.Collaborators));
    }

    private bool HasRolePermission(PermissionRole role, string memberId, bool isCollaboratorRole)
    {
        if (role == null || (role.Units.Count == 0 && string.IsNullOrEmpty(role.Rank) && role.Users.Count == 0))
        {
            return false;
        }

        // Check if user is explicitly listed - immediate access
        if (role.Users.Contains(memberId))
        {
            return true;
        }

        // If not in Users list, check (Units AND Rank) logic
        var hasUnitPermission = true;
        var hasRankPermission = true;

        // Check unit permission if units are specified
        if (role.Units.Count > 0)
        {
            if (role.ExpandToSubUnits)
            {
                // When ExpandToSubUnits is true:
                // - For viewers: check selected units + all child units (descendants)
                // - For collaborators: check selected units + all parent units (ancestors) + all child units (descendants)
                hasUnitPermission = isCollaboratorRole
                    ? role.Units.Any(unitId => unitsService.AnyParentHasMember(unitId, memberId) || unitsService.AnyChildHasMember(unitId, memberId))
                    : role.Units.Any(unitId => unitsService.AnyChildHasMember(unitId, memberId));
            }
            else
            {
                // When ExpandToSubUnits is false: check only the selected units (exact match)
                hasUnitPermission = role.Units.Any(unitId => unitsService.HasMember(unitId, memberId));
            }
        }

        // Check rank permission if rank is specified
        if (!string.IsNullOrEmpty(role.Rank))
        {
            var account = accountService.GetUserAccount();
            var memberRank = account.Rank;
            hasRankPermission = ranksService.IsSuperiorOrEqual(memberRank, role.Rank);
        }

        // Both unit and rank conditions must be met if both are specified
        return hasUnitPermission && hasRankPermission;
    }

    private static bool HasPermissionRoleData(PermissionRole role)
    {
        return role != null && (role.Units.Count > 0 || !string.IsNullOrEmpty(role.Rank) || role.Users.Count > 0);
    }

    private static PermissionRole ClonePermissionRole(PermissionRole role)
    {
        if (role == null)
        {
            return new PermissionRole();
        }

        return new PermissionRole
        {
            Units = [..role.Units],
            Users = [..role.Users],
            Rank = role.Rank ?? string.Empty,
            ExpandToSubUnits = role.ExpandToSubUnits
        };
    }
}
