using System.Collections.Generic;
using UKSF.Api.Base.Models;

namespace UKSF.Api.Tests.Common {
    public record TestDataModel : MongoObject {
        public Dictionary<string, object> Dictionary { get; set; } = new();
        public string Name { get; set; }
        public List<object> Stuff { get; set; }
    }
}
