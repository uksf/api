using System.Collections.Generic;
using UKSF.Api.Models;

namespace UKSF.Tests.Common {
    public class MockDataModel : DatabaseObject {
        public string Name;
        public List<object> Stuff;
    }
}
