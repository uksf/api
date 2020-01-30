using System;

namespace UKSF.Api.Interfaces.Integrations {
    public interface IPipeManager : IDisposable {
        void Start();
    }
}
