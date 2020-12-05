using System;
using System.Collections.Generic;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using UKSF.Api.Base.Models;

namespace UKSF.Api.Command.Models {
    public enum ReviewState {
        APPROVED,
        REJECTED,
        PENDING,
        ERROR
    }

    public static class CommandRequestType {
        public const string AUXILIARY_TRANSFER = "Axuiliary Transfer";
        public const string DEMOTION = "Demotion";
        public const string DISCHARGE = "Discharge";
        public const string INDIVIDUAL_ROLE = "Individual Role";
        public const string LOA = "Loa";
        public const string PROMOTION = "Promotion";
        public const string REINSTATE_MEMBER = "Reinstate Member";
        public const string TRANSFER = "Transfer";
        public const string UNIT_REMOVAL = "Unit Removal";
        public const string UNIT_ROLE = "Unit Role";
    }

    public record CommandRequest : MongoObject {
        public DateTime DateCreated { get; set; }
        public string DisplayFrom { get; set; }
        public string DisplayRecipient { get; set; }
        public string DisplayRequester { get; set; }
        public string DisplayValue { get; set; }
        public string Reason { get; set; }
        public string Type { get; set; }
        [BsonRepresentation(BsonType.ObjectId)] public string Recipient { get; set; }
        [BsonRepresentation(BsonType.ObjectId)] public string Requester { get; set; }
        public Dictionary<string, ReviewState> Reviews { get; set; } = new();
        public string SecondaryValue { get; set; }
        public string Value { get; set; }
        public CommandRequest() => DateCreated = DateTime.Now;
    }
}
