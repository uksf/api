using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;
using UKSF.Api.AppStart;
using UKSF.Api.ArmaServer.DataContext;
using UKSF.Api.Core.Services;
using UKSF.Api.Extensions;
using UKSF.Api.Integrations.Discord.Services;
using UKSF.Api.Integrations.Teamspeak.Services;
using UKSF.Api.Modpack.Services;
using Xunit;

namespace UKSF.Api.Tests.AppStart;

public class VerifyModeStartupTests
{
    private readonly Mock<ITeamspeakManagerService> _teamspeak = new();
    private readonly Mock<IDiscordActivationService> _discord = new();
    private readonly Mock<ISchedulerService> _scheduler = new();
    private readonly Mock<IBuildsService> _builds = new();
    private readonly Mock<IModpackService> _modpack = new();

    private IServiceProvider Provider(string verifyFlag)
    {
        _builds.Setup(x => x.CancelInterruptedBuilds()).ReturnsAsync(0);
        _discord.Setup(x => x.Activate()).Returns(Task.CompletedTask);
        return new ServiceCollection().AddSingleton(new VerifyMode(verifyFlag, null))
                                      .AddSingleton(_teamspeak.Object)
                                      .AddSingleton(_discord.Object)
                                      .AddSingleton(_scheduler.Object)
                                      .AddSingleton(_builds.Object)
                                      .AddScoped(_ => _modpack.Object)
                                      .BuildServiceProvider();
    }

    [Fact]
    public void StartIntegrations_InVerifyMode_StartsNothingExternal()
    {
        Provider("1").StartIntegrations();

        _teamspeak.Verify(x => x.Start(), Times.Never);
        _discord.Verify(x => x.Activate(), Times.Never);
        _scheduler.Verify(x => x.Load(), Times.Never);
        _builds.Verify(x => x.CancelInterruptedBuilds(), Times.Never);
        _modpack.Verify(x => x.RunQueuedBuilds(), Times.Never);
    }

    [Fact]
    public void StartIntegrations_OutsideVerifyMode_StartsEveryIntegration()
    {
        Provider(null).StartIntegrations();

        _teamspeak.Verify(x => x.Start(), Times.Once);
        _discord.Verify(x => x.Activate(), Times.Once);
        _scheduler.Verify(x => x.Load(), Times.Once);
        _builds.Verify(x => x.CancelInterruptedBuilds(), Times.Once);
        _modpack.Verify(x => x.RunQueuedBuilds(), Times.Once);
    }

    [Fact]
    public void StopIntegrations_InVerifyMode_StopsNothingExternal()
    {
        Provider("1").StopIntegrations();

        _teamspeak.Verify(x => x.Stop(), Times.Never);
        _discord.Verify(x => x.Deactivate(), Times.Never);
    }

    [Fact]
    public void RemoveExternalHostedServices_RemovesEveryListedServiceFromTheRealRegistrations()
    {
        var services = RealRegistrations();
        var hostedBefore = HostedImplementations(services);
        hostedBefore.Should().Contain(VerifyModeServices.ExternalHostedServices, "the list must name services the API really registers");

        services.RemoveExternalHostedServices();

        var hostedAfter = HostedImplementations(services);
        hostedAfter.Should().NotContain(VerifyModeServices.ExternalHostedServices);
        hostedAfter.Should().Contain(typeof(MissionStatsIndexes));
        hostedAfter.Should().HaveCount(hostedBefore.Count - VerifyModeServices.ExternalHostedServices.Count);
    }

    private static IServiceCollection RealRegistrations()
    {
        IServiceCollection services = new ServiceCollection();
        foreach (var descriptor in DependencyInjectionTests.RealRegistrations)
        {
            services.Add(descriptor);
        }

        return services;
    }

    private static List<Type> HostedImplementations(IServiceCollection services)
    {
        return services.Where(x => x.ServiceType == typeof(IHostedService)).Select(x => x.ImplementationType).Where(x => x is not null).Cast<Type>().ToList();
    }
}
