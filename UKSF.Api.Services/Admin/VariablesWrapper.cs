using Microsoft.Extensions.DependencyInjection;
using UKSF.Api.Interfaces.Data.Cached;

namespace UKSF.Api.Services.Admin {
    public static class VariablesWrapper {
        public static IVariablesDataService VariablesDataService() => ServiceWrapper.ServiceProvider.GetService<IVariablesDataService>();
    }
}
