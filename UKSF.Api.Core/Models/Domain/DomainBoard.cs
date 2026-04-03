using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace UKSF.Api.Core.Models.Domain;

public class DomainBoard : MongoObject
{
    public string Name { get; set; }

    [BsonRepresentation(BsonType.ObjectId)]
    public string CreatedBy { get; set; }

    public DateTime CreatedAt { get; set; }
    public bool IsDeleted { get; set; }
    public BoardPermissions Permissions { get; set; } = new();
    public List<string> Labels { get; set; } = [];
    public List<BoardColumn> Columns { get; set; } = [];
}

public class BoardPermissions
{
    [BsonRepresentation(BsonType.ObjectId)]
    public List<string> Units { get; set; } = [];

    [BsonRepresentation(BsonType.ObjectId)]
    public List<string> Members { get; set; } = [];

    public bool ExpandToSubUnits { get; set; } = true;
}

public class BoardColumn
{
    public BoardColumnKey Key { get; set; }
    public string Name { get; set; }
    public List<BoardCard> Cards { get; set; } = [];
}

public enum BoardColumnKey
{
    Todo,
    Blocked,
    InProgress,
    Review,
    Done
}

public class BoardCard : MongoObject
{
    public string Title { get; set; }
    public string Detail { get; set; }
    public List<string> Labels { get; set; } = [];

    [BsonRepresentation(BsonType.ObjectId)]
    public string AssigneeId { get; set; }

    [BsonRepresentation(BsonType.ObjectId)]
    public string CreatedBy { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    public int Order { get; set; }

    [BsonRepresentation(BsonType.ObjectId)]
    public string CommentThreadId { get; set; }

    public List<BoardCardActivity> Activity { get; set; } = [];
}

public class BoardCardActivity
{
    [BsonRepresentation(BsonType.ObjectId)]
    public string UserId { get; set; }

    public DateTime Timestamp { get; set; }
    public string Description { get; set; }
}
