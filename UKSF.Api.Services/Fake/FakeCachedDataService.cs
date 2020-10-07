using UKSF.Api.Models;

namespace UKSF.Api.Services.Fake {
    public class FakeCachedDataService<T> : FakeDataService<T> where T : DatabaseObject {
        public void Refresh() { }
    }
}
