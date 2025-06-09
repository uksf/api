using MongoDB.Bson;
using UKSF.Api.Core;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Core.Services;

namespace UKSF.Api.Services;

public interface IDocumentPermissionsService
{
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

    public bool CanContextView(DomainMetadataWithPermissions metadataWithPermissions)
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

    public bool CanContextCollaborate(DomainMetadataWithPermissions metadataWithPermissions)
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

        // Check collaborator (has both read and write access)
        if (HasPermission(effectivePermissions.Collaborators, CurrentUserId, true))
        {
            return true;
        }

        // Check viewer (read-only access)
        return HasPermission(effectivePermissions.Viewers, CurrentUserId, false);
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
        return HasPermission(effectivePermissions.Collaborators, CurrentUserId, true);
    }

    private DocumentPermissions CalculateEffectivePermissions(DomainMetadataWithPermissions metadataWithPermissions)
    {
        var effectivePermissions = new DocumentPermissions();

        // Start with inherited permissions (always returns a valid object now)
        var inheritedPermissions = GetInheritedPermissions(metadataWithPermissions);
        effectivePermissions.Viewers = ClonePermission(inheritedPermissions.Viewers);
        effectivePermissions.Collaborators = ClonePermission(inheritedPermissions.Collaborators);

        // Override with custom permissions where they exist
        var customPermissions = metadataWithPermissions.Permissions;
        if (customPermissions != null)
        {
            if (HasPermissionsData(customPermissions.Viewers))
            {
                effectivePermissions.Viewers = ClonePermission(customPermissions.Viewers);
            }

            if (HasPermissionsData(customPermissions.Collaborators))
            {
                effectivePermissions.Collaborators = ClonePermission(customPermissions.Collaborators);
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
        var viewerPermissions = GetInheritedPermissions(parentId, true);
        var collaboratorPermissions = GetInheritedPermissions(parentId, false);

        // Always return a permissions object to ensure consistent API responses
        return new DocumentPermissions
        {
            Viewers = viewerPermissions ?? new DocumentPermission(), Collaborators = collaboratorPermissions ?? new DocumentPermission()
        };
    }

    private DocumentPermission GetInheritedPermissions(string parentId, bool isViewer)
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

        // Check if this parent has the specific permissions set
        var permission = isViewer ? parentFolder.Permissions?.Viewers : parentFolder.Permissions?.Collaborators;

        return HasPermissionsData(permission) ? ClonePermission(permission) : GetInheritedPermissions(parentFolder.Parent, isViewer);
    }

    private bool HasPermission(DocumentPermission permission, string memberId, bool isCollaborator)
    {
        if (permission == null || (permission.Units.Count == 0 && string.IsNullOrEmpty(permission.Rank) && permission.Members.Count == 0))
        {
            return false;
        }

        // Check if user is explicitly listed - immediate access
        if (permission.Members.Contains(memberId))
        {
            return true;
        }

        // If not in Members list, check (Units AND Rank) logic
        var hasUnitPermission = true;
        var hasRankPermission = true;

        // Check unit permission if units are specified
        if (permission.Units.Count > 0)
        {
            if (permission.ExpandToSubUnits)
            {
                // When ExpandToSubUnits is true:
                // - For viewers: check selected units + all child units (descendants)
                // - For collaborators: check selected units + all parent units (ancestors) + all child units (descendants)
                hasUnitPermission = isCollaborator
                    ? permission.Units.Any(unitId => unitsService.AnyParentHasMember(unitId, memberId) || unitsService.AnyChildHasMember(unitId, memberId))
                    : permission.Units.Any(unitId => unitsService.AnyChildHasMember(unitId, memberId));
            }
            else
            {
                // When ExpandToSubUnits is false: check only the selected units (exact match)
                hasUnitPermission = permission.Units.Any(unitId => unitsService.HasMember(unitId, memberId));
            }
        }

        // Check rank permission if rank is specified
        if (!string.IsNullOrEmpty(permission.Rank))
        {
            var account = accountService.GetUserAccount();
            var memberRank = account.Rank;
            hasRankPermission = ranksService.IsSuperiorOrEqual(memberRank, permission.Rank);
        }

        // Both unit and rank conditions must be met if both are specified
        return hasUnitPermission && hasRankPermission;
    }

    private static bool HasPermissionsData(DocumentPermission permission)
    {
        return permission != null && (permission.Units.Count > 0 || !string.IsNullOrEmpty(permission.Rank) || permission.Members.Count > 0);
    }

    private static DocumentPermission ClonePermission(DocumentPermission permission)
    {
        if (permission == null)
        {
            return new DocumentPermission();
        }

        return new DocumentPermission
        {
            Units = [..permission.Units],
            Members = [..permission.Members],
            Rank = permission.Rank ?? string.Empty,
            ExpandToSubUnits = permission.ExpandToSubUnits
        };
    }
}
