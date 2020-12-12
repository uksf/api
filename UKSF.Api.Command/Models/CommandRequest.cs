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

    public class CommandRequest : MongoObject {
        public DateTime DateCreated;
        public string DisplayFrom;
        public string DisplayRecipient;
        public string DisplayRequester;
        public string DisplayValue;
        public string Reason;
        public string Type;
        [BsonRepresentation(BsonType.ObjectId)] public string Recipient;
        [BsonRepresentation(BsonType.ObjectId)] public string Requester;
        public Dictionary<string, ReviewState> Reviews = new();
        public string SecondaryValue;
        public string Value;
        public CommandRequest() => DateCreated = DateTime.Now;
    }
}
