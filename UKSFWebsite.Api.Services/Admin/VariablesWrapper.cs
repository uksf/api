using Microsoft.Extensions.DependencyInjection;
using UKSFWebsite.Api.Interfaces.Data.Cached;

namespace UKSFWebsite.Api.Services.Admin {
    public static class VariablesWrapper {
        public static IVariablesDataService VariablesDataService() => ServiceWrapper.ServiceProvider.GetService<IVariablesDataService>();
    }
}