using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using UKSF.Api.Base;
using UKSF.Api.Shared;

namespace UKSF.Api.Tests.Common {
    public class DependencyInjectionTestsBase {
        protected readonly ServiceCollection Services;
        protected readonly IConfigurationRoot Configuration;
        protected readonly IHostEnvironment HostEnvironment;

        protected DependencyInjectionTestsBase() {
            Mock<IHostEnvironment> mockHostEnvironment = new();
            mockHostEnvironment.Setup(x => x.EnvironmentName).Returns(Environments.Development);

            Services = new ServiceCollection();
            Configuration = TestConfigurationProvider.GetTestConfiguration();
            HostEnvironment = mockHostEnvironment.Object;

            Services.TryAddTransient(typeof(ILogger<>), typeof(Logger<>));
            Services.TryAddTransient(typeof(ILoggerFactory), typeof(LoggerFactory));
            Services.AddSingleton<IConfiguration>(Configuration);

            Services.AddUksfBase(Configuration, HostEnvironment);
            Services.AddUksfShared();
        }
    }
}
