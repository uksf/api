using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using UKSF.Api.Admin;
using UKSF.Api.Personnel;
using UKSF.Api.Teamspeak;
using UKSF.Api.Teamspeak.Controllers;
using UKSF.Api.Teamspeak.EventHandlers;
using UKSF.Api.Teamspeak.ScheduledActions;
using UKSF.Api.Tests.Common;
using Xunit;

namespace UKSF.Api.Integrations.Teamspeak.Tests;

public class DependencyInjectionTests : DependencyInjectionTestsBase
{
    public DependencyInjectionTests()
    {
        Services.AddUksfAdmin();
        Services.AddUksfPersonnel();
        Services.AddUksfIntegrationTeamspeak();
    }

    [Fact]
    public void When_resolving_controllers()
    {
        Services.AddTransient<OperationsController>();
        Services.AddTransient<TeamspeakController>();
        var serviceProvider = Services.BuildServiceProvider();

        serviceProvider.GetRequiredService<OperationsController>().Should().NotBeNull();
        serviceProvider.GetRequiredService<TeamspeakController>().Should().NotBeNull();
    }

    [Fact]
    public void When_resolving_event_handlers()
    {
        Services.AddTransient<TeamspeakEventHandler>();
        Services.AddTransient<TeamspeakServerEventHandler>();
        var serviceProvider = Services.BuildServiceProvider();

        serviceProvider.GetRequiredService<TeamspeakEventHandler>().Should().NotBeNull();
        serviceProvider.GetRequiredService<TeamspeakServerEventHandler>().Should().NotBeNull();
    }

    [Fact]
    public void When_resolving_scheduled_actions()
    {
        Services.AddTransient<ActionTeamspeakSnapshot>();
        var serviceProvider = Services.BuildServiceProvider();

        serviceProvider.GetRequiredService<ActionTeamspeakSnapshot>().Should().NotBeNull();
    }
}
