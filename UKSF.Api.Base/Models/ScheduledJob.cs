using System;

namespace UKSF.Api.Base.Models {
    public class ScheduledJob : DatabaseObject {
        public string action;
        public string actionParameters;
        public TimeSpan interval;
        public DateTime next;
        public bool repeat;
    }
}
