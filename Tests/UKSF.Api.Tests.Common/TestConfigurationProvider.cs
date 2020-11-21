using Microsoft.Extensions.Configuration;

namespace UKSF.Api.Tests.Common {
    public static class TestConfigurationProvider {
        public static IConfigurationRoot GetTestConfiguration() => new ConfigurationBuilder().AddJsonFile("appsettings.json").Build();
    }
}
