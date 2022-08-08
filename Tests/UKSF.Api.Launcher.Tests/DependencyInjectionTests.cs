using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using UKSF.Api.Admin;
using UKSF.Api.Launcher.Controllers;
using UKSF.Api.Personnel;
using UKSF.Api.Tests.Common;
using Xunit;

namespace UKSF.Api.Launcher.Tests;

public class DependencyInjectionTests : DependencyInjectionTestsBase
{
    public DependencyInjectionTests()
    {
        Services.AddUksfAdmin();
        Services.AddUksfPersonnel();
        Services.AddUksfLauncher();
    }

    [Fact]
    public void When_resolving_controllers()
    {
        Services.AddTransient<LauncherController>();
        var serviceProvider = Services.BuildServiceProvider();

        serviceProvider.GetRequiredService<LauncherController>().Should().NotBeNull();
    }
}
