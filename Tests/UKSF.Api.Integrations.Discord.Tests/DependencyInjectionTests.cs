using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using UKSF.Api.Admin;
using UKSF.Api.Discord;
using UKSF.Api.Discord.Controllers;
using UKSF.Api.Discord.EventHandlers;
using UKSF.Api.Personnel;
using UKSF.Api.Tests.Common;
using Xunit;

namespace UKSF.Api.Integrations.Discord.Tests;

public class DependencyInjectionTests : DependencyInjectionTestsBase
{
    public DependencyInjectionTests()
    {
        Services.AddUksfAdmin();
        Services.AddUksfPersonnel();
        Services.AddUksfIntegrationDiscord();
    }

    [Fact]
    public void When_resolving_controllers()
    {
        Services.AddTransient<DiscordController>();
        var serviceProvider = Services.BuildServiceProvider();

        serviceProvider.GetRequiredService<DiscordController>().Should().NotBeNull();
    }

    [Fact]
    public void When_resolving_event_handlers()
    {
        Services.AddTransient<DiscordAccountEventHandler>();
        var serviceProvider = Services.BuildServiceProvider();

        serviceProvider.GetRequiredService<DiscordAccountEventHandler>().Should().NotBeNull();
    }
}
