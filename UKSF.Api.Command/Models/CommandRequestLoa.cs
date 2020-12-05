using System;

namespace UKSF.Api.Command.Models {
    public record CommandRequestLoa : CommandRequest {
        public string Emergency { get; set; }
        public DateTime End { get; set; }
        public string Late { get; set; }
        public DateTime Start { get; set; }
    }
}
