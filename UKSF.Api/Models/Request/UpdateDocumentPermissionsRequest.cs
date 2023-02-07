using UKSF.Api.Core.Models;

namespace UKSF.Api.Models.Request;

public class UpdateDocumentPermissionsRequest
{
    public DocumentPermissions ReadPermissions { get; set; }
    public DocumentPermissions WritePermissions { get; set; }
}
