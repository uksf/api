using UKSF.Api.Core.Models.Domain;

namespace UKSF.Api.Models.Response;

public class FolderMetadataResponse
{
    public string Id { get; set; }
    public string Parent { get; set; }
    public string Name { get; set; }
    public string FullPath { get; set; }
    public DateTime Created { get; set; }
    public string Creator { get; set; }

    // Legacy permissions (keep for backwards compatibility)
    public DocumentPermissions ReadPermissions { get; set; } = new();
    public DocumentPermissions WritePermissions { get; set; } = new();

    // NEW: Role-based permissions
    public string Owner { get; set; }
    public RoleBasedDocumentPermissions RoleBasedPermissions { get; set; } = new();
    public RoleBasedDocumentPermissions EffectivePermissions { get; set; } = new();
    public RoleBasedDocumentPermissions InheritedPermissions { get; set; } = new();

    public IEnumerable<DocumentMetadataResponse> Documents { get; set; }

    public bool CanWrite { get; set; }
}

public class DocumentMetadataResponse
{
    public string Id { get; set; }
    public string Folder { get; set; }
    public string Name { get; set; }
    public string FullPath { get; set; }
    public DateTime Created { get; set; }
    public DateTime LastUpdated { get; set; }
    public string Creator { get; set; }

    // Legacy permissions (keep for backwards compatibility)
    public DocumentPermissions ReadPermissions { get; set; } = new();
    public DocumentPermissions WritePermissions { get; set; } = new();

    // NEW: Role-based permissions
    public string Owner { get; set; }
    public RoleBasedDocumentPermissions RoleBasedPermissions { get; set; } = new();
    public RoleBasedDocumentPermissions EffectivePermissions { get; set; } = new();
    public RoleBasedDocumentPermissions InheritedPermissions { get; set; } = new();

    public bool CanWrite { get; set; }
}
