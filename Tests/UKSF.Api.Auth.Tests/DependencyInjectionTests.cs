using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using UKSF.Api.Admin;
using UKSF.Api.Auth.Controllers;
using UKSF.Api.Personnel;
using UKSF.Api.Tests.Common;
using Xunit;

namespace UKSF.Api.Auth.Tests
{
    public class DependencyInjectionTests : DependencyInjectionTestsBase
    {
        public DependencyInjectionTests()
        {
            Services.AddUksfAdmin();
            Services.AddUksfPersonnel();
            Services.AddUksfAuth(Configuration);
        }

        [Fact]
        public void When_resolving_controllers()
        {
            Services.AddTransient<AuthController>();
            ServiceProvider serviceProvider = Services.BuildServiceProvider();

            serviceProvider.GetRequiredService<AuthController>().Should().NotBeNull();
        }
    }
}
