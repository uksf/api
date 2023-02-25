using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace UKSF.Api.Core.Models;

public class DomainMetadataWithPermissions : MongoObject
{
    public string Name { get; set; }
    public string FullPath { get; set; }
    public DateTime Created { get; set; }

    [BsonRepresentation(BsonType.ObjectId)]
    public string Creator { get; set; }

    public DocumentPermissions ReadPermissions { get; set; } = new();
    public DocumentPermissions WritePermissions { get; set; } = new();
}

public class DomainDocumentFolderMetadata : DomainMetadataWithPermissions
{
    [BsonRepresentation(BsonType.ObjectId)]
    public string Parent { get; set; }

    public List<DomainDocumentMetadata> Documents { get; set; } = new();
}

public class DomainDocumentMetadata : DomainMetadataWithPermissions
{
    [BsonRepresentation(BsonType.ObjectId)]
    public string Folder { get; set; }
    public DateTime LastUpdated { get; set; }
}

public class DocumentPermissions
{
    [BsonRepresentation(BsonType.ObjectId)]
    public List<string> Units { get; set; } = new();
    public string Rank { get; set; }
    public bool SelectedUnitsOnly { get; set; }
}
