using System;

namespace UKSF.Api.Command.Models {
    public class CommandRequestLoa : CommandRequest {
        public string emergency;
        public DateTime end;
        public string late;
        public DateTime start;
    }
}
