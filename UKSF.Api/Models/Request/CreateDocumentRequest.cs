using UKSF.Api.Core.Models;

namespace UKSF.Api.Models.Request;

public class CreateFolderRequest
{
    public string Parent { get; set; }
    public string Name { get; set; }
    public DocumentPermissions ReadPermissions { get; set; } = new();
    public DocumentPermissions WritePermissions { get; set; } = new();
}

public class CreateDocumentRequest
{
    public string Name { get; set; }
    public DocumentPermissions ReadPermissions { get; set; } = new();
    public DocumentPermissions WritePermissions { get; set; } = new();
}
