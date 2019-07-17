using UKSFWebsite.Api.Services.Abstraction;

namespace UKSFWebsite.Api.Services.Debug {
    public class FakePipeManager : IPipeManager {
        public void Dispose() { }

        public void Start() { }
    }
}
