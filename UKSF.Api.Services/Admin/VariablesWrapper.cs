using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using UKSF.Api.Interfaces.Data.Cached;
using UKSF.Api.Services.Common;

namespace UKSF.Api.Services.Admin {
    [ExcludeFromCodeCoverage]
    public static class VariablesWrapper {
        public static IVariablesDataService VariablesDataService() => ServiceWrapper.ServiceProvider.GetService<IVariablesDataService>();
    }
}
