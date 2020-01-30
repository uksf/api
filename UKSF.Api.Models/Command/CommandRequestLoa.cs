using System;

namespace UKSF.Api.Models.Command {
    public class CommandRequestLoa : CommandRequest {
        public string emergency;
        public DateTime end;
        public string late;
        public DateTime start;
    }
}
