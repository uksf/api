using System;

namespace UKSFWebsite.Api.Services.Abstraction {
    public interface IPipeManager : IDisposable {
        void Start();
    }
}
