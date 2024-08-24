using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace UKSF.Api.Core.Models.Domain;

public enum ReviewState
{
    Approved,
    Rejected,
    Pending,
    Error
}

public static class CommandRequestType
{
    public const string AuxiliaryTransfer = "Axuiliary Transfer";
    public const string Demotion = "Demotion";
    public const string Discharge = "Discharge";
    public const string IndividualRole = "Individual Role";
    public const string Loa = "Loa";
    public const string Promotion = "Promotion";
    public const string ReinstateMember = "Reinstate Member";
    public const string Transfer = "Transfer";
    public const string UnitRemoval = "Unit Removal";
    public const string UnitRole = "Unit Role";
}

public class DomainCommandRequest : MongoObject
{
    public DateTime DateCreated { get; set; } = DateTime.UtcNow;
    public string DisplayFrom { get; set; }
    public string DisplayRecipient { get; set; }
    public string DisplayRequester { get; set; }
    public string DisplayValue { get; set; }
    public string Reason { get; set; }

    [BsonRepresentation(BsonType.ObjectId)]
    public string Recipient { get; set; }

    [BsonRepresentation(BsonType.ObjectId)]
    public string Requester { get; set; }

    public Dictionary<string, ReviewState> Reviews { get; set; } = new();
    public string SecondaryValue { get; set; }
    public string Type { get; set; }
    public string Value { get; set; }
}
