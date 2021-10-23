using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using UKSF.Api.Admin;
using UKSF.Api.Command.Controllers;
using UKSF.Api.Command.EventHandlers;
using UKSF.Api.Personnel;
using UKSF.Api.Tests.Common;
using Xunit;

namespace UKSF.Api.Command.Tests
{
    public class DependencyInjectionTests : DependencyInjectionTestsBase
    {
        public DependencyInjectionTests()
        {
            Services.AddUksfAdmin();
            Services.AddUksfPersonnel();
            Services.AddUksfCommand();
        }

        [Fact]
        public void When_resolving_controllers()
        {
            Services.AddTransient<CommandRequestsController>();
            Services.AddTransient<CommandRequestsCreationController>();
            Services.AddTransient<DischargesController>();
            Services.AddTransient<OperationOrderController>();
            Services.AddTransient<OperationReportController>();
            var serviceProvider = Services.BuildServiceProvider();

            serviceProvider.GetRequiredService<CommandRequestsController>().Should().NotBeNull();
            serviceProvider.GetRequiredService<CommandRequestsCreationController>().Should().NotBeNull();
            serviceProvider.GetRequiredService<DischargesController>().Should().NotBeNull();
            serviceProvider.GetRequiredService<OperationOrderController>().Should().NotBeNull();
            serviceProvider.GetRequiredService<OperationReportController>().Should().NotBeNull();
        }

        [Fact]
        public void When_resolving_event_handlers()
        {
            Services.AddTransient<CommandRequestEventHandler>();
            var serviceProvider = Services.BuildServiceProvider();

            serviceProvider.GetRequiredService<CommandRequestEventHandler>().Should().NotBeNull();
        }
    }
}
