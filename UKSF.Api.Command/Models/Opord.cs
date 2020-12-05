using System;
using UKSF.Api.Base.Models;

namespace UKSF.Api.Command.Models {
    public record Opord : MongoObject {
        public string Description { get; set; }
        public DateTime End { get; set; }
        public string Map { get; set; }
        public string Name { get; set; }
        public DateTime Start { get; set; }
        public string Type { get; set; }
    }
}
