using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using UKSF.Api.Admin.Controllers;
using UKSF.Api.Admin.EventHandlers;
using UKSF.Api.Admin.ScheduledActions;
using UKSF.Api.Tests.Common;
using Xunit;

namespace UKSF.Api.Admin.Tests
{
    public class DependencyInjectionTests : DependencyInjectionTestsBase
    {
        public DependencyInjectionTests()
        {
            Services.AddUksfAdmin();
        }

        [Fact]
        public void When_resolving_controllers()
        {
            Services.AddTransient<DataController>();
            Services.AddTransient<VariablesController>();
            Services.AddTransient<VersionController>();
            ServiceProvider serviceProvider = Services.BuildServiceProvider();

            serviceProvider.GetRequiredService<DataController>().Should().NotBeNull();
            serviceProvider.GetRequiredService<VariablesController>().Should().NotBeNull();
            serviceProvider.GetRequiredService<VersionController>().Should().NotBeNull();
        }

        [Fact]
        public void When_resolving_event_handlers()
        {
            Services.AddTransient<LogDataEventHandler>();
            ServiceProvider serviceProvider = Services.BuildServiceProvider();

            serviceProvider.GetRequiredService<LogDataEventHandler>().Should().NotBeNull();
        }

        [Fact]
        public void When_resolving_scheduled_actions()
        {
            Services.AddTransient<ActionPruneLogs>();
            ServiceProvider serviceProvider = Services.BuildServiceProvider();

            serviceProvider.GetRequiredService<ActionPruneLogs>().Should().NotBeNull();
        }
    }
}
