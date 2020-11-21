using System.Collections.Generic;

namespace UKSF.Api.Tests.Common {
    public record TestComplexDataModel : TestDataModel {
        public TestDataModel Data;
        public List<TestDataModel> DataList;
        public List<string> List;
    }
}
