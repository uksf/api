using System;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using UKSF.Api.Base.Models;
using UKSF.Api.Personnel.Models;

namespace UKSF.Api.Command.Models
{
    public enum LoaReviewState
    {
        PENDING,
        APPROVED,
        REJECTED
    }

    public class DomainLoa : MongoObject
    {
        public bool Emergency;
        public DateTime End;
        public bool Late;
        public string Reason;
        [BsonRepresentation(BsonType.ObjectId)] public string Recipient;
        public DateTime Start;
        public LoaReviewState State;
        public DateTime Submitted;
    }

    public class DomainLoaWithAccount : DomainLoa
    {
        public DomainAccount Account;
    }

    public class Loa
    {
        public bool Emergency;
        public DateTime End;
        public string Id;
        public bool InChainOfCommand;
        public bool Late;
        public bool LongTerm;
        public string Name;
        public string Reason;
        public DateTime Start;
        public LoaReviewState State;
        public DateTime Submitted;
    }
}
