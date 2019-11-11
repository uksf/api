using System;

namespace UKSFWebsite.Api.Interfaces.Integrations {
    public interface IPipeManager : IDisposable {
        void Start();
    }
}
