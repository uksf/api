using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using UKSF.Api.Admin;
using UKSF.Api.ArmaMissions;
using UKSF.Api.ArmaServer;
using UKSF.Api.Discord;
using UKSF.Api.Modpack.Controllers;
using UKSF.Api.Modpack.EventHandlers;
using UKSF.Api.Modpack.ScheduledActions;
using UKSF.Api.Personnel;
using UKSF.Api.Tests.Common;
using Xunit;

namespace UKSF.Api.Modpack.Tests
{
    public class DependencyInjectionTests : DependencyInjectionTestsBase
    {
        public DependencyInjectionTests()
        {
            Services.AddUksfAdmin();
            Services.AddUksfPersonnel();
            Services.AddUksfArmaMissions();
            Services.AddUksfArmaServer();
            Services.AddUksfIntegrationDiscord();
            Services.AddUksfModpack();
        }

        [Fact]
        public void When_resolving_controllers()
        {
            Services.AddTransient<GithubController>();
            Services.AddTransient<IssueController>();
            Services.AddTransient<ModpackController>();
            var serviceProvider = Services.BuildServiceProvider();

            serviceProvider.GetRequiredService<GithubController>().Should().NotBeNull();
            serviceProvider.GetRequiredService<IssueController>().Should().NotBeNull();
            serviceProvider.GetRequiredService<ModpackController>().Should().NotBeNull();
        }

        [Fact]
        public void When_resolving_event_handlers()
        {
            Services.AddTransient<BuildsEventHandler>();
            var serviceProvider = Services.BuildServiceProvider();

            serviceProvider.GetRequiredService<BuildsEventHandler>().Should().NotBeNull();
        }

        [Fact]
        public void When_resolving_scheduled_actions()
        {
            Services.AddTransient<ActionPruneBuilds>();
            var serviceProvider = Services.BuildServiceProvider();

            serviceProvider.GetRequiredService<ActionPruneBuilds>().Should().NotBeNull();
        }
    }
}
