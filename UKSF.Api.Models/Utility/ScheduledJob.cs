using System;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace UKSF.Api.Models.Utility {
    public enum ScheduledJobType {
        NORMAL,
        TEAMSPEAK_SNAPSHOT,
        LOG_PRUNE,
        INTEGRATION,
        DISCORD_VOTE_ANNOUNCEMENT
    }

    public class ScheduledJob {
        public string action;
        public string actionParameters;
        [BsonId, BsonRepresentation(BsonType.ObjectId)] public string id;
        public TimeSpan interval;
        public DateTime next;
        public bool repeat;
        public ScheduledJobType type = ScheduledJobType.NORMAL;
    }
}
