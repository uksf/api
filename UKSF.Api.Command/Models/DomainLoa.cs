using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using UKSF.Api.Base.Models;
using UKSF.Api.Personnel.Models;

namespace UKSF.Api.Command.Models;

public enum LoaReviewState
{
    PENDING,
    APPROVED,
    REJECTED
}

public class DomainLoa : MongoObject
{
    public bool Emergency { get; set; }
    public DateTime End { get; set; }
    public bool Late { get; set; }
    public string Reason { get; set; }

    [BsonRepresentation(BsonType.ObjectId)]
    public string Recipient { get; set; }

    public DateTime Start { get; set; }
    public LoaReviewState State { get; set; }
    public DateTime Submitted { get; set; }
}

public class DomainLoaWithAccount : DomainLoa
{
    public DomainAccount Account { get; set; }
    public DomainRank Rank { get; set; }
    public DomainUnit Unit { get; set; }
}

public class Loa
{
    public bool Emergency { get; set; }
    public DateTime End { get; set; }
    public string Id { get; set; }
    public bool InChainOfCommand { get; set; }
    public bool Late { get; set; }
    public bool LongTerm { get; set; }
    public string Name { get; set; }
    public string Reason { get; set; }
    public DateTime Start { get; set; }
    public LoaReviewState State { get; set; }
    public DateTime Submitted { get; set; }
}
