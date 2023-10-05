using UKSF.Api.Core.Models;

namespace UKSF.Api.Models.Response;

public class FolderMetadataResponse
{
    public string Id { get; set; }
    public string Parent { get; set; }
    public string Name { get; set; }
    public string FullPath { get; set; }
    public DateTime Created { get; set; }
    public string Creator { get; set; }

    public DocumentPermissions ReadPermissions { get; set; } = new();
    public DocumentPermissions WritePermissions { get; set; } = new();
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

    public DocumentPermissions ReadPermissions { get; set; } = new();
    public DocumentPermissions WritePermissions { get; set; } = new();

    public bool CanRead { get; set; }
    public bool CanWrite { get; set; }
}
