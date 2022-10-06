using Microsoft.Extensions.Configuration;

namespace UKSF.Api.Tests.Common;

public static class TestConfigurationProvider
{
    public static IConfigurationRoot GetTestConfiguration()
    {
        return new ConfigurationBuilder().AddJsonFile("appsettings.Tests.json").Build();
    }
}
