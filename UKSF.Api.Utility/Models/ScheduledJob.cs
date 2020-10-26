using System;
using UKSF.Api.Base.Models;

namespace UKSF.Api.Utility.Models {
    public class ScheduledJob : DatabaseObject {
        public string action;
        public string actionParameters;
        public TimeSpan interval;
        public DateTime next;
        public bool repeat;
    }
}
