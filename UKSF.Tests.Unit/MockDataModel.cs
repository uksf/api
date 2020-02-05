using UKSF.Api.Models;

namespace UKSF.Tests.Unit {
    public class MockDataModel : MongoObject {
        public string Name;

        public MockDataModel() { }

        protected MockDataModel(string id) : base(id) { }
    }
}
