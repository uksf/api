namespace UKSFWebsite.Api.Services.Debug {
    public class FakeCachedDataService<T> : FakeDataService<T> {
        public void Refresh() { }
    }
}
