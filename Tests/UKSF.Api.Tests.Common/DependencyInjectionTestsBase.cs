using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace UKSF.Api.Tests.Common {
    public class DependencyInjectionTestsBase {
        protected readonly ServiceCollection Services;
        protected readonly IConfigurationRoot TestConfiguration;

        protected DependencyInjectionTestsBase() {
            Services = new ServiceCollection();
            TestConfiguration = TestConfigurationProvider.GetTestConfiguration();

            Services.AddSingleton<IConfiguration>(TestConfiguration);
        }
    }
}
