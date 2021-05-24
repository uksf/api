using System;

namespace UKSF.Api.Command.Models
{
    public class CommandRequestLoa : CommandRequest
    {
        public string Emergency;
        public DateTime End;
        public string Late;
        public DateTime Start;
    }
}
