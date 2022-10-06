using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using UKSF.Api.Controllers;
using UKSF.Api.EventHandlers;
using UKSF.Api.Extensions;
using UKSF.Api.Middleware;
using UKSF.Api.ScheduledActions;
using UKSF.Api.Tests.Common;
using Xunit;

namespace UKSF.Api.Tests;

public class DependencyInjectionTests : DependencyInjectionTestsBase
{
    public DependencyInjectionTests()
    {
        Services.AddUksf(Configuration, HostEnvironment);
    }

    [Fact]
    public void When_resolving_controllers()
    {
        Services.AddTransient<AccountsController>();
        Services.AddTransient<ApplicationsController>();
        Services.AddTransient<CommentThreadController>();
        Services.AddTransient<TeamspeakConnectionController>();
        Services.AddTransient<DiscordCodeController>();
        Services.AddTransient<DiscordConnectionController>();
        Services.AddTransient<DisplayNameController>();
        Services.AddTransient<NotificationsController>();
        Services.AddTransient<RanksController>();
        Services.AddTransient<RecruitmentController>();
        Services.AddTransient<RolesController>();
        Services.AddTransient<ServiceRecordsController>();
        Services.AddTransient<SteamCodeController>();
        Services.AddTransient<SteamConnectionController>();
        Services.AddTransient<UnitsController>();
        Services.AddTransient<AuthController>();
        Services.AddTransient<CommandRequestsController>();
        Services.AddTransient<CommandRequestsCreationController>();
        Services.AddTransient<DataController>();
        Services.AddTransient<DischargesController>();
        Services.AddTransient<LoaController>();
        Services.AddTransient<LoggingController>();
        Services.AddTransient<ModsController>();
        Services.AddTransient<OperationOrderController>();
        Services.AddTransient<OperationReportController>();
        Services.AddTransient<VariablesController>();
        Services.AddTransient<VersionController>();
        var serviceProvider = Services.BuildServiceProvider();

        serviceProvider.GetRequiredService<AuthController>().Should().NotBeNull();
        serviceProvider.GetRequiredService<CommandRequestsController>().Should().NotBeNull();
        serviceProvider.GetRequiredService<CommandRequestsCreationController>().Should().NotBeNull();
        serviceProvider.GetRequiredService<DataController>().Should().NotBeNull();
        serviceProvider.GetRequiredService<DischargesController>().Should().NotBeNull();
        serviceProvider.GetRequiredService<LoaController>().Should().NotBeNull();
        serviceProvider.GetRequiredService<LoggingController>().Should().NotBeNull();
        serviceProvider.GetRequiredService<ModsController>().Should().NotBeNull();
        serviceProvider.GetRequiredService<OperationOrderController>().Should().NotBeNull();
        serviceProvider.GetRequiredService<OperationReportController>().Should().NotBeNull();
        serviceProvider.GetRequiredService<VariablesController>().Should().NotBeNull();
        serviceProvider.GetRequiredService<VersionController>().Should().NotBeNull();
        serviceProvider.GetRequiredService<AccountsController>().Should().NotBeNull();
        serviceProvider.GetRequiredService<ApplicationsController>().Should().NotBeNull();
        serviceProvider.GetRequiredService<CommentThreadController>().Should().NotBeNull();
        serviceProvider.GetRequiredService<TeamspeakConnectionController>().Should().NotBeNull();
        serviceProvider.GetRequiredService<DiscordCodeController>().Should().NotBeNull();
        serviceProvider.GetRequiredService<DiscordConnectionController>().Should().NotBeNull();
        serviceProvider.GetRequiredService<DisplayNameController>().Should().NotBeNull();
        serviceProvider.GetRequiredService<NotificationsController>().Should().NotBeNull();
        serviceProvider.GetRequiredService<RanksController>().Should().NotBeNull();
        serviceProvider.GetRequiredService<RecruitmentController>().Should().NotBeNull();
        serviceProvider.GetRequiredService<RolesController>().Should().NotBeNull();
        serviceProvider.GetRequiredService<ServiceRecordsController>().Should().NotBeNull();
        serviceProvider.GetRequiredService<SteamCodeController>().Should().NotBeNull();
        serviceProvider.GetRequiredService<TeamspeakConnectionController>().Should().NotBeNull();
        serviceProvider.GetRequiredService<TeamspeakConnectionController>().Should().NotBeNull();
    }

    [Fact]
    public void When_resolving_event_handlers()
    {
        Services.AddTransient<AccountDataEventHandler>();
        Services.AddTransient<CommentThreadEventHandler>();
        Services.AddTransient<NotificationsEventHandler>();
        Services.AddTransient<CommandRequestEventHandler>();
        Services.AddTransient<LogDataEventHandler>();
        Services.AddTransient<UksfLoggerEventHandler>();
        var serviceProvider = Services.BuildServiceProvider();

        serviceProvider.GetRequiredService<CommandRequestEventHandler>().Should().NotBeNull();
        serviceProvider.GetRequiredService<LogDataEventHandler>().Should().NotBeNull();
        serviceProvider.GetRequiredService<UksfLoggerEventHandler>().Should().NotBeNull();

        serviceProvider.GetRequiredService<AccountDataEventHandler>().Should().NotBeNull();
        serviceProvider.GetRequiredService<CommentThreadEventHandler>().Should().NotBeNull();
        serviceProvider.GetRequiredService<NotificationsEventHandler>().Should().NotBeNull();
    }

    [Fact]
    public void When_resolving_filters()
    {
        Services.AddTransient<ExceptionHandler>();
        var serviceProvider = Services.BuildServiceProvider();

        serviceProvider.GetRequiredService<ExceptionHandler>().Should().NotBeNull();
    }

    [Fact]
    public void When_resolving_scheduled_actions()
    {
        Services.AddTransient<ActionPruneNotifications>();
        Services.AddTransient<ActionPruneLogs>();
        var serviceProvider = Services.BuildServiceProvider();

        serviceProvider.GetRequiredService<ActionPruneLogs>().Should().NotBeNull();
        serviceProvider.GetRequiredService<ActionPruneNotifications>().Should().NotBeNull();
    }
}
