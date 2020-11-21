using System;
using UKSF.Api.Base.Models;

namespace UKSF.Api.Shared.Models {
    public record ScheduledJob : MongoObject {
        public string Action;
        public string ActionParameters;
        public TimeSpan Interval;
        public DateTime Next;
        public bool Repeat;
    }
}
