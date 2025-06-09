using MongoDB.Bson;
using UKSF.Api.Core;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Core.Services;

namespace UKSF.Api.Services;

public interface IDocumentPermissionsService
{
    bool DoesContextHaveReadPermission(DomainMetadataWithPermissions metadataWithPermissions);
    bool DoesContextHaveWritePermission(DomainMetadataWithPermissions metadataWithPermissions);
    bool CanContextView(DomainMetadataWithPermissions metadataWithPermissions);
    bool CanContextCollaborate(DomainMetadataWithPermissions metadataWithPermissions);
    DocumentPermissions GetEffectivePermissions(DomainMetadataWithPermissions metadataWithPermissions);
    DocumentPermissions GetInheritedPermissions(DomainMetadataWithPermissions metadataWithPermissions);
    DocumentPermissions GetInheritedPermissionsFromHierarchy(string parentId);
}

public class PermissionsCache
{
    private readonly Dictionary<string, bool> _collaborateCache = new();
    private readonly Dictionary<string, DocumentPermissions> _effectivePermissionsCache = new();
    private readonly Dictionary<string, DocumentPermissions> _hierarchyCache = new();
    private readonly Dictionary<string, DocumentPermissions> _inheritedPermissionsCache = new();
    private readonly Dictionary<string, bool> _viewCache = new();

    public bool TryGetReadPermission(string key, out bool hasPermission)
    {
        return _viewCache.TryGetValue(key, out hasPermission);
    }

    public void SetReadPermission(string key, bool hasPermission)
    {
        _viewCache[key] = hasPermission;
    }

    public bool TryGetWritePermission(string key, out bool hasPermission)
    {
        return _collaborateCache.TryGetValue(key, out hasPermission);
    }

    public void SetWritePermission(string key, bool hasPermission)
    {
        _collaborateCache[key] = hasPermission;
    }

    public bool TryGetEffectivePermissions(string key, out DocumentPermissions permissions)
    {
        return _effectivePermissionsCache.TryGetValue(key, out permissions);
    }

    public void SetEffectivePermissions(string key, DocumentPermissions permissions)
    {
        _effectivePermissionsCache[key] = permissions;
    }

    public bool TryGetInheritedPermissions(string key, out DocumentPermissions permissions)
    {
        return _inheritedPermissionsCache.TryGetValue(key, out permissions);
    }

    public void SetInheritedPermissions(string key, DocumentPermissions permissions)
    {
        _inheritedPermissionsCache[key] = permissions;
    }

    public bool TryGetHierarchyPermissions(string key, out DocumentPermissions permissions)
    {
        return _hierarchyCache.TryGetValue(key, out permissions);
    }

    public void SetHierarchyPermissions(string key, DocumentPermissions permissions)
    {
        _hierarchyCache[key] = permissions;
    }
}

public class DocumentPermissionsService(
    IHttpContextService httpContextService,
    IUnitsService unitsService,
    IRanksService ranksService,
    IAccountService accountService,
    IDocumentFolderMetadataContext documentFolderMetadataContext
) : IDocumentPermissionsService
{
    private readonly PermissionsCache _cache = new();
    private string _currentUserId;
    private bool? _isSuperadmin;

    private string CurrentUserId => _currentUserId ??= httpContextService.GetUserId();
    private bool IsSuperadmin => _isSuperadmin ??= httpContextService.UserHasPermission(Permissions.Superadmin);

    public bool DoesContextHaveReadPermission(DomainMetadataWithPermissions metadataWithPermissions)
    {
        var cacheKey = $"read:{metadataWithPermissions.Id}:{CurrentUserId}";
        if (_cache.TryGetReadPermission(cacheKey, out var cachedResult))
        {
            return cachedResult;
        }

        var result = CalculateViewPermission(metadataWithPermissions);
        _cache.SetReadPermission(cacheKey, result);
        return result;
    }

    public bool DoesContextHaveWritePermission(DomainMetadataWithPermissions metadataWithPermissions)
    {
        var cacheKey = $"write:{metadataWithPermissions.Id}:{CurrentUserId}";
        if (_cache.TryGetWritePermission(cacheKey, out var cachedResult))
        {
            return cachedResult;
        }

        var result = CalculateCollaboratePermission(metadataWithPermissions);
        _cache.SetWritePermission(cacheKey, result);
        return result;
    }

    public bool CanContextView(DomainMetadataWithPermissions metadataWithPermissions)
    {
        return DoesContextHaveReadPermission(metadataWithPermissions);
    }

    public bool CanContextCollaborate(DomainMetadataWithPermissions metadataWithPermissions)
    {
        return DoesContextHaveWritePermission(metadataWithPermissions);
    }

    public DocumentPermissions GetEffectivePermissions(DomainMetadataWithPermissions metadataWithPermissions)
    {
        var cacheKey = $"effective:{metadataWithPermissions.Id}";
        if (_cache.TryGetEffectivePermissions(cacheKey, out var cachedResult))
        {
            return cachedResult;
        }

        var result = CalculateEffectivePermissions(metadataWithPermissions);
        _cache.SetEffectivePermissions(cacheKey, result);
        return result;
    }

    public DocumentPermissions GetInheritedPermissions(DomainMetadataWithPermissions metadataWithPermissions)
    {
        var cacheKey = $"inherited:{metadataWithPermissions.Id}";
        if (_cache.TryGetInheritedPermissions(cacheKey, out var cachedResult))
        {
            return cachedResult;
        }

        var result = CalculateInheritedPermissions(metadataWithPermissions);
        _cache.SetInheritedPermissions(cacheKey, result);
        return result;
    }

    public DocumentPermissions GetInheritedPermissionsFromHierarchy(string parentId)
    {
        var cacheKey = $"hierarchy:{parentId}";
        if (_cache.TryGetHierarchyPermissions(cacheKey, out var cachedResult))
        {
            return cachedResult;
        }

        var result = CalculateInheritedPermissionsFromParentHierarchy(parentId);
        _cache.SetHierarchyPermissions(cacheKey, result);
        return result;
    }

    private bool CalculateViewPermission(DomainMetadataWithPermissions metadataWithPermissions)
    {
        if (IsSuperadmin)
        {
            return true;
        }

        // Check ownership
        if (!string.IsNullOrEmpty(metadataWithPermissions.Owner) && metadataWithPermissions.Owner == CurrentUserId)
        {
            return true;
        }

        var effectivePermissions = GetEffectivePermissions(metadataWithPermissions);

        // Check collaborator role (has both read and write access)
        if (HasRolePermission(effectivePermissions.Collaborators, CurrentUserId, true))
        {
            return true;
        }

        // Check viewer role (read-only access)
        return HasRolePermission(effectivePermissions.Viewers, CurrentUserId, false);
    }

    private bool CalculateCollaboratePermission(DomainMetadataWithPermissions metadataWithPermissions)
    {
        if (IsSuperadmin)
        {
            return true;
        }

        // Check ownership
        if (!string.IsNullOrEmpty(metadataWithPermissions.Owner) && metadataWithPermissions.Owner == CurrentUserId)
        {
            return true;
        }

        var effectivePermissions = GetEffectivePermissions(metadataWithPermissions);

        // Only collaborators have write access
        return HasRolePermission(effectivePermissions.Collaborators, CurrentUserId, true);
    }

    private DocumentPermissions CalculateEffectivePermissions(DomainMetadataWithPermissions metadataWithPermissions)
    {
        var effectivePermissions = new DocumentPermissions();

        // Start with inherited permissions (always returns a valid object now)
        var inheritedPermissions = GetInheritedPermissions(metadataWithPermissions);
        effectivePermissions.Viewers = ClonePermissionRole(inheritedPermissions.Viewers);
        effectivePermissions.Collaborators = ClonePermissionRole(inheritedPermissions.Collaborators);

        // Override with custom permissions where they exist
        var customPermissions = metadataWithPermissions.Permissions;
        if (customPermissions != null)
        {
            if (HasPermissionRoleData(customPermissions.Viewers))
            {
                effectivePermissions.Viewers = ClonePermissionRole(customPermissions.Viewers);
            }

            if (HasPermissionRoleData(customPermissions.Collaborators))
            {
                effectivePermissions.Collaborators = ClonePermissionRole(customPermissions.Collaborators);
            }
        }

        return effectivePermissions;
    }

    private DocumentPermissions CalculateInheritedPermissions(DomainMetadataWithPermissions metadataWithPermissions)
    {
        return metadataWithPermissions switch
        {
            // Only folders can have parents, documents inherit from their folder
            DomainDocumentFolderMetadata folderMetadata => GetInheritedPermissionsFromParentHierarchy(folderMetadata.Parent),
            DomainDocumentMetadata documentMetadata     => GetInheritedPermissionsFromParentHierarchy(documentMetadata.Folder),
            _                                           => new DocumentPermissions()
        };

        // Return empty permissions for unknown types
    }

    private DocumentPermissions GetInheritedPermissionsFromParentHierarchy(string parentId)
    {
        var cacheKey = $"hierarchy:{parentId}";
        if (_cache.TryGetHierarchyPermissions(cacheKey, out var cachedResult))
        {
            return cachedResult;
        }

        var result = CalculateInheritedPermissionsFromParentHierarchy(parentId);
        _cache.SetHierarchyPermissions(cacheKey, result);
        return result;
    }

    private DocumentPermissions CalculateInheritedPermissionsFromParentHierarchy(string parentId)
    {
        var viewerPermissions = GetInheritedRolePermissions(parentId, true);
        var collaboratorPermissions = GetInheritedRolePermissions(parentId, false);

        // Always return a permissions object to ensure consistent API responses
        return new DocumentPermissions { Viewers = viewerPermissions ?? new PermissionRole(), Collaborators = collaboratorPermissions ?? new PermissionRole() };
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
        var rolePermissions = isViewerRole ? parentFolder.Permissions?.Viewers : parentFolder.Permissions?.Collaborators;

        return HasPermissionRoleData(rolePermissions) ? ClonePermissionRole(rolePermissions) : GetInheritedRolePermissions(parentFolder.Parent, isViewerRole);
    }

    private bool HasRolePermission(PermissionRole role, string memberId, bool isCollaboratorRole)
    {
        if (role == null || (role.Units.Count == 0 && string.IsNullOrEmpty(role.Rank) && role.Members.Count == 0))
        {
            return false;
        }

        // Check if user is explicitly listed - immediate access
        if (role.Members.Contains(memberId))
        {
            return true;
        }

        // If not in Members list, check (Units AND Rank) logic
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
        return role != null && (role.Units.Count > 0 || !string.IsNullOrEmpty(role.Rank) || role.Members.Count > 0);
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
            Members = [..role.Members],
            Rank = role.Rank ?? string.Empty,
            ExpandToSubUnits = role.ExpandToSubUnits
        };
    }
}
