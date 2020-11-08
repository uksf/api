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

    public class CommandRequest : DatabaseObject {
        public DateTime dateCreated;
        public string displayFrom;
        public string displayRecipient;
        public string displayRequester;
        public string displayValue;
        public string reason, type;
        [BsonRepresentation(BsonType.ObjectId)] public string recipient;
        [BsonRepresentation(BsonType.ObjectId)] public string requester;
        public Dictionary<string, ReviewState> reviews = new Dictionary<string, ReviewState>();
        public string secondaryValue;
        public string value;
        public CommandRequest() => dateCreated = DateTime.Now;
    }
}
