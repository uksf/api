using System;

namespace UKSF.Api.Models.Utility {
    public enum ScheduledJobType {
        NORMAL,
        TEAMSPEAK_SNAPSHOT,
        LOG_PRUNE,
        INTEGRATION,
        DISCORD_VOTE_ANNOUNCEMENT
    }

    public class ScheduledJob : MongoObject {
        public string action;
        public string actionParameters;
        public TimeSpan interval;
        public DateTime next;
        public bool repeat;
        public ScheduledJobType type = ScheduledJobType.NORMAL;
    }
}