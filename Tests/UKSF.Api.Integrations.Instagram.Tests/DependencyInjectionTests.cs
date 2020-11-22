using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using UKSF.Api.Admin;
using UKSF.Api.Integrations.Instagram.Controllers;
using UKSF.Api.Integrations.Instagram.ScheduledActions;
using UKSF.Api.Tests.Common;
using Xunit;

namespace UKSF.Api.Integrations.Instagram.Tests {
    public class DependencyInjectionTests : DependencyInjectionTestsBase {
        public DependencyInjectionTests() {
            Services.AddUksfAdmin();
            Services.AddUksfIntegrationInstagram();
        }

        [Fact]
        public void When_resolving_controllers() {
            Services.AddTransient<InstagramController>();
            ServiceProvider serviceProvider = Services.BuildServiceProvider();

            serviceProvider.GetRequiredService<InstagramController>().Should().NotBeNull();
        }

        [Fact]
        public void When_resolving_scheduled_actions() {
            Services.AddTransient<ActionInstagramImages>();
            Services.AddTransient<ActionInstagramToken>();
            ServiceProvider serviceProvider = Services.BuildServiceProvider();

            serviceProvider.GetRequiredService<ActionInstagramImages>().Should().NotBeNull();
            serviceProvider.GetRequiredService<ActionInstagramToken>().Should().NotBeNull();
        }
    }
}
