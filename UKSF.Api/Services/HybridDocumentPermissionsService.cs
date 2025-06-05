using UKSF.Api.Core.Models.Domain;

namespace UKSF.Api.Services;

public interface IHybridDocumentPermissionsService
{
    bool DoesContextHaveReadPermission(DomainMetadataWithPermissions metadata);
    bool DoesContextHaveWritePermission(DomainMetadataWithPermissions metadata);
    RoleBasedDocumentPermissions GetEffectivePermissions(DomainMetadataWithPermissions metadata);
    RoleBasedDocumentPermissions GetInheritedPermissions(DomainMetadataWithPermissions metadata);
}

public class HybridDocumentPermissionsService(
    IDocumentPermissionsService legacyDocumentPermissionsService,
    IRoleBasedDocumentPermissionsService roleBasedDocumentPermissionsService
) : IHybridDocumentPermissionsService
{
    public bool DoesContextHaveReadPermission(DomainMetadataWithPermissions metadata)
    {
        // If role-based permissions are defined (not empty), use those
        if (HasRoleBasedPermissions(metadata))
        {
            return roleBasedDocumentPermissionsService.DoesContextHaveReadPermission(metadata);
        }

        // Fall back to legacy permissions
        return legacyDocumentPermissionsService.DoesContextHaveReadPermission(metadata);
    }

    public bool DoesContextHaveWritePermission(DomainMetadataWithPermissions metadata)
    {
        // If role-based permissions are defined (not empty), use those
        if (HasRoleBasedPermissions(metadata))
        {
            return roleBasedDocumentPermissionsService.DoesContextHaveWritePermission(metadata);
        }

        // Fall back to legacy permissions
        return legacyDocumentPermissionsService.DoesContextHaveWritePermission(metadata);
    }

    public RoleBasedDocumentPermissions GetEffectivePermissions(DomainMetadataWithPermissions metadata)
    {
        if (HasRoleBasedPermissions(metadata))
        {
            return roleBasedDocumentPermissionsService.GetEffectivePermissions(metadata);
        }

        // Convert legacy permissions to role-based format
        return ConvertLegacyToRoleBased(metadata);
    }

    public RoleBasedDocumentPermissions GetInheritedPermissions(DomainMetadataWithPermissions metadata)
    {
        if (HasRoleBasedPermissions(metadata))
        {
            return roleBasedDocumentPermissionsService.GetInheritedPermissions(metadata);
        }

        // For legacy, there's no inheritance concept, so return empty but properly initialized permissions
        return new RoleBasedDocumentPermissions { Viewers = new PermissionRole(), Collaborators = new PermissionRole() };
    }

    private bool HasRoleBasedPermissions(DomainMetadataWithPermissions metadata)
    {
        var rbp = metadata.RoleBasedPermissions;
        if (rbp == null) return false;

        return (!string.IsNullOrEmpty(rbp.Viewers?.Rank) ||
                rbp.Viewers?.Units?.Any() == true ||
                !string.IsNullOrEmpty(rbp.Collaborators?.Rank) ||
                rbp.Collaborators?.Units?.Any() == true);
    }

    private RoleBasedDocumentPermissions ConvertLegacyToRoleBased(DomainMetadataWithPermissions metadata)
    {
        // Simple conversion from legacy to role-based format
        var result = new RoleBasedDocumentPermissions { Viewers = new PermissionRole(), Collaborators = new PermissionRole() };

        if (metadata.ReadPermissions != null && (!string.IsNullOrEmpty(metadata.ReadPermissions.Rank) || metadata.ReadPermissions.Units?.Any() == true))
        {
            result.Viewers.Rank = metadata.ReadPermissions.Rank ?? string.Empty;
            result.Viewers.Units = metadata.ReadPermissions.Units?.ToList() ?? new List<string>();
        }

        if (metadata.WritePermissions != null && (!string.IsNullOrEmpty(metadata.WritePermissions.Rank) || metadata.WritePermissions.Units?.Any() == true))
        {
            result.Collaborators.Rank = metadata.WritePermissions.Rank ?? string.Empty;
            result.Collaborators.Units = metadata.WritePermissions.Units?.ToList() ?? new List<string>();
        }

        return result;
    }
}
