using System.Collections.Generic;
using UKSF.Api.Models;

namespace UKSF.Tests.Unit.Common {
    public class MockDataModel : DatabaseObject {
        public string Name;
        public List<object> Stuff;
    }
}
