using System.ComponentModel.DataAnnotations;
using UKSF.Api.Core.Models.Domain;

namespace UKSF.Api.Models.Request;

public class CreateFolderRequest
{
    [Required]
    public string Parent { get; set; }

    [Required]
    public string Name { get; set; }

    [Required]
    public string Owner { get; set; }

    public DocumentPermissions Permissions { get; set; } = new();
}

public class CreateDocumentRequest
{
    [Required]
    public string Name { get; set; }

    [Required]
    public string Owner { get; set; }

    public DocumentPermissions Permissions { get; set; } = new();
}
