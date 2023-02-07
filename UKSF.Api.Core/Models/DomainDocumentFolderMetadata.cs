using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace UKSF.Api.Core.Models;

public class DomainDocumentFolderMetadata : MongoObject
{
    [BsonRepresentation(BsonType.ObjectId)]
    public string Parent { get; set; }

    public string Name { get; set; }
    public string FullPath { get; set; }
    public DateTime Created { get; set; }

    [BsonRepresentation(BsonType.ObjectId)]
    public string Creator { get; set; }

    public DocumentPermissions ReadPermissions { get; set; } = new();
    public DocumentPermissions WritePermissions { get; set; } = new();

    public List<DomainDocumentMetadata> Documents { get; set; } = new();
}

public class DomainDocumentMetadata : MongoObject
{
    [BsonRepresentation(BsonType.ObjectId)]
    public string Folder { get; set; }

    public string Name { get; set; }
    public string FullPath { get; set; }
    public DateTime Created { get; set; }
    public DateTime LastUpdated { get; set; }

    [BsonRepresentation(BsonType.ObjectId)]
    public string Creator { get; set; }

    public DocumentPermissions ReadPermissions { get; set; } = new();
    public DocumentPermissions WritePermissions { get; set; } = new();
}

public class DocumentPermissions
{
    [BsonRepresentation(BsonType.ObjectId)]
    public List<string> Units { get; set; } = new();

    [BsonRepresentation(BsonType.ObjectId)]
    public string Rank { get; set; }
}
