namespace UKSFWebsite.Api.Services.Fake {
    public class FakeCachedDataService<T, TData> : FakeDataService<T, TData> {
        public void Refresh() { }
    }
}
