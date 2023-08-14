using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using UKSF.Api.Core;
using UKSF.Api.Extensions;
using UKSF.Api.Tests.Common;
using Xunit;

namespace UKSF.Api.Tests;

public class DependencyInjectionTests
{
    private static readonly DependencyInjectionTestHelper Setup;
    public static readonly IEnumerable<object[]> ResolvableTypes;

    static DependencyInjectionTests()
    {
        Mock<IHostEnvironment> mockHostEnvironment = new();
        mockHostEnvironment.Setup(x => x.EnvironmentName).Returns(Environments.Development);

        var configuration = TestConfigurationProvider.GetTestConfiguration();
        var hostEnvironment = mockHostEnvironment.Object;

        Setup = DependencyInjectionTestHelper.FromServiceCollection(
            services =>
            {
                services.TryAddTransient(typeof(ILogger<>), typeof(Logger<>));
                services.TryAddTransient(typeof(ILoggerFactory), typeof(LoggerFactory));
                services.AddSingleton<IConfiguration>(configuration);

                services.AddUksfShared(configuration, hostEnvironment);
                services.AddUksf(configuration, hostEnvironment);

                return services;
            }
        );

        ResolvableTypes = Setup.ResolvableTypes.Select(x => new object[] { x });
    }

    [Theory]
    [MemberData(nameof(ResolvableTypes))]
    public void DependencyInjection(Type serviceType)
    {
        Setup.ServiceProvider.GetService(serviceType).Should().NotBeNull();
    }
}
