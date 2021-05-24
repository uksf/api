using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using UKSF.Api.Admin;
using UKSF.Api.ArmaMissions;
using UKSF.Api.ArmaServer.Controllers;
using UKSF.Api.Personnel;
using UKSF.Api.Tests.Common;
using Xunit;

namespace UKSF.Api.ArmaServer.Tests
{
    public class DependencyInjectionTests : DependencyInjectionTestsBase
    {
        public DependencyInjectionTests()
        {
            Services.AddUksfAdmin();
            Services.AddUksfPersonnel();
            Services.AddUksfArmaMissions();
            Services.AddUksfArmaServer();
        }

        [Fact]
        public void When_resolving_controllers()
        {
            Services.AddTransient<GameServersController>();
            ServiceProvider serviceProvider = Services.BuildServiceProvider();

            serviceProvider.GetRequiredService<GameServersController>().Should().NotBeNull();
        }
    }
}
