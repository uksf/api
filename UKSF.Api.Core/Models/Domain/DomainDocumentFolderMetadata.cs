using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace UKSF.Api.Core.Models.Domain;

public class DomainMetadataWithPermissions : MongoObject
{
    public string Name { get; set; }
    public string FullPath { get; set; }
    public DateTime Created { get; set; }

    [BsonRepresentation(BsonType.ObjectId)]
    public string Creator { get; set; }

    [BsonRepresentation(BsonType.ObjectId)]
    public string Owner { get; set; }

    public DocumentPermissions Permissions { get; set; } = new();
}

public class DomainDocumentFolderMetadata : DomainMetadataWithPermissions
{
    [BsonRepresentation(BsonType.ObjectId)]
    public string Parent { get; set; }

    public List<DomainDocumentMetadata> Documents { get; set; } = [];
}

public class DomainDocumentMetadata : DomainMetadataWithPermissions
{
    [BsonRepresentation(BsonType.ObjectId)]
    public string Folder { get; set; }

    public DateTime LastUpdated { get; set; }
}

public class DocumentPermissions
{
    public DocumentPermission Viewers { get; set; } = new();
    public DocumentPermission Collaborators { get; set; } = new();
}

public class DocumentPermission
{
    [BsonRepresentation(BsonType.ObjectId)]
    public List<string> Units { get; set; } = [];

    [BsonRepresentation(BsonType.ObjectId)]
    public List<string> Members { get; set; } = [];

    public string Rank { get; set; } = string.Empty;
    public bool ExpandToSubUnits { get; set; } = true;
}
