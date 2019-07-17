using System;

namespace UKSFWebsite.Api.Models.CommandRequests {
    public class CommandRequestLoa : CommandRequest {
        public string emergency;
        public DateTime start;
        public DateTime end;
        public string late;
    }
}
