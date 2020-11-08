using System.Collections.Generic;
using UKSF.Api.Base.Models;

namespace UKSF.Tests.Common {
    public class TestDataModel : DatabaseObject {
        public string Name;
        public List<object> Stuff;
        public Dictionary<string, object> Dictionary = new Dictionary<string, object>();
    }
}
