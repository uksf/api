using System;
using System.Collections.Generic;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using UKSF.Api.Base.Models;

namespace UKSF.Api.Command.Models
{
    public enum ReviewState
    {
        APPROVED,
        REJECTED,
        PENDING,
        ERROR
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

    public class CommandRequest : MongoObject
    {
        public DateTime DateCreated;
        public string DisplayFrom;
        public string DisplayRecipient;
        public string DisplayRequester;
        public string DisplayValue;
        public string Reason;
        [BsonRepresentation(BsonType.ObjectId)] public string Recipient;
        [BsonRepresentation(BsonType.ObjectId)] public string Requester;
        public Dictionary<string, ReviewState> Reviews = new();
        public string SecondaryValue;
        public string Type;
        public string Value;

        public CommandRequest()
        {
            DateCreated = DateTime.UtcNow;
        }
    }
}
