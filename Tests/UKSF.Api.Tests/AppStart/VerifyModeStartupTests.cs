using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MongoDB.Driver;
using Moq;
using UKSF.Api.AppStart;
using UKSF.Api.ArmaServer.DataContext;
using UKSF.Api.ArmaServer.Npc.Observability;
using UKSF.Api.ArmaServer.Npc.Services;
using UKSF.Api.ArmaServer.ScheduledActions;
using UKSF.Api.ArmaServer.Services;
using UKSF.Api.Backups.Services;
using UKSF.Api.Core;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Models;
using UKSF.Api.Core.ScheduledActions;
using UKSF.Api.Core.Services;
using UKSF.Api.Extensions;
using UKSF.Api.Integrations.Discord.Services;
using UKSF.Api.Integrations.Teamspeak.Services;
using UKSF.Api.Modpack.Services;
using UKSF.Api.Services;
using Xunit;

namespace UKSF.Api.Tests.AppStart;

public class VerifyModeStartupTests
{
    private static readonly Type[] ForbiddenInVerifyMode =
    [
        typeof(GameServerProcessManagerStartup),
        typeof(GameDataExportRecoveryStartup),
        typeof(DevRunRecoveryStartup),
        typeof(BackupStartupCheck),
        typeof(NpcVoiceReconciler),
        typeof(NpcMoodGenWorker),
        typeof(NpcWarmKeeper),
        typeof(NpcTraceMaintenance),
        typeof(NpcIndexes)
    ];

    private static readonly Type[] AllowedInVerifyMode = [typeof(MissionStatsIndexes), typeof(NpcTraceWriter)];

    private readonly Mock<ITeamspeakManagerService> _teamspeak = new();
    private readonly Mock<IDiscordActivationService> _discord = new();
    private readonly Mock<ISchedulerService> _scheduler = new();
    private readonly Mock<IBuildsService> _builds = new();
    private readonly Mock<IModpackService> _modpack = new();
    private readonly Mock<IMigrationContext> _migrations = new();
    private readonly Mock<ISelfCreatingScheduledAction> _selfCreating = new();

    private IServiceProvider Provider(string verifyFlag)
    {
        _builds.Setup(x => x.CancelInterruptedBuilds()).ReturnsAsync(0);
        _discord.Setup(x => x.Activate()).Returns(Task.CompletedTask);
        _discord.Setup(x => x.Deactivate()).Returns(Task.CompletedTask);
        _migrations.Setup(x => x.GetSingle(It.IsAny<Func<Migration, bool>>())).Returns(new Migration());
        return new ServiceCollection().AddSingleton(new VerifyMode(verifyFlag, null))
                                      .AddSingleton(_teamspeak.Object)
                                      .AddSingleton(_discord.Object)
                                      .AddSingleton(_scheduler.Object)
                                      .AddSingleton(_builds.Object)
                                      .AddScoped(_ => _modpack.Object)
                                      .AddSingleton(new MigrationUtility(_migrations.Object, new Mock<IMongoDatabase>().Object, new Mock<IUksfLogger>().Object))
                                      .AddSingleton<IEnumerable<ISelfCreatingScheduledAction>>([_selfCreating.Object])
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
    public void StopIntegrations_OutsideVerifyMode_StopsTeamspeakAndDiscord()
    {
        Provider(null).StopIntegrations();

        _teamspeak.Verify(x => x.Stop(), Times.Once);
        _discord.Verify(x => x.Deactivate(), Times.Once);
    }

    [Fact]
    public void RunMigrations_InVerifyMode_NeverTouchesTheMigrationState()
    {
        Provider("1").RunStartupMigrations();

        _migrations.Verify(x => x.GetSingle(It.IsAny<Func<Migration, bool>>()), Times.Never);
    }

    [Fact]
    public void RunMigrations_OutsideVerifyMode_ChecksTheMigrationState()
    {
        Provider(null).RunStartupMigrations();

        _migrations.Verify(x => x.GetSingle(It.IsAny<Func<Migration, bool>>()), Times.Once);
    }

    [Fact]
    public void CreateSelfScheduledJobs_InVerifyMode_CreatesNoJobs()
    {
        Provider("1").CreateSelfScheduledJobs();

        _selfCreating.Verify(x => x.CreateSelf(), Times.Never);
    }

    [Fact]
    public void CreateSelfScheduledJobs_OutsideVerifyMode_CreatesJobs()
    {
        Provider(null).CreateSelfScheduledJobs();

        _selfCreating.Verify(x => x.CreateSelf(), Times.Once);
    }

    [Fact]
    public void RemoveExternalHostedServices_RemovesEveryForbiddenServiceFromTheRealRegistrations()
    {
        var services = RealRegistrations();
        HostedImplementations(services).Should().Contain(ForbiddenInVerifyMode.Concat(AllowedInVerifyMode));

        services.RemoveExternalHostedServices();

        var hostedAfter = HostedImplementations(services);
        hostedAfter.Should().NotContain(ForbiddenInVerifyMode);
        hostedAfter.Should().Contain(AllowedInVerifyMode);
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
