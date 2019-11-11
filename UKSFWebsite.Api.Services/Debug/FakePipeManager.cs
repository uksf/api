using UKSFWebsite.Api.Interfaces.Integrations;

namespace UKSFWebsite.Api.Services.Debug {
    public class FakePipeManager : IPipeManager {
        public void Dispose() { }

        public void Start() { }
    }
}
