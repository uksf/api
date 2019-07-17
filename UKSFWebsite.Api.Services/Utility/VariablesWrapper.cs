using Microsoft.Extensions.DependencyInjection;
using UKSFWebsite.Api.Services.Abstraction;

namespace UKSFWebsite.Api.Services.Utility {
    public static class VariablesWrapper {
        public static IVariablesService VariablesService() => ServiceWrapper.ServiceProvider.GetService<IVariablesService>();
    }
}
