using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using UKSF.Api.Admin;
using UKSF.Api.Personnel.Controllers;
using UKSF.Api.Personnel.EventHandlers;
using UKSF.Api.Personnel.ScheduledActions;
using UKSF.Api.Tests.Common;
using Xunit;

namespace UKSF.Api.Personnel.Tests
{
    public class DependencyInjectionTests : DependencyInjectionTestsBase
    {
        public DependencyInjectionTests()
        {
            Services.AddUksfAdmin();
            Services.AddUksfPersonnel();
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
            ServiceProvider serviceProvider = Services.BuildServiceProvider();

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
            ServiceProvider serviceProvider = Services.BuildServiceProvider();

            serviceProvider.GetRequiredService<AccountDataEventHandler>().Should().NotBeNull();
            serviceProvider.GetRequiredService<CommentThreadEventHandler>().Should().NotBeNull();
            serviceProvider.GetRequiredService<NotificationsEventHandler>().Should().NotBeNull();
        }

        [Fact]
        public void When_resolving_scheduled_actions()
        {
            Services.AddTransient<ActionDeleteExpiredConfirmationCode>();
            Services.AddTransient<ActionPruneNotifications>();
            ServiceProvider serviceProvider = Services.BuildServiceProvider();

            serviceProvider.GetRequiredService<ActionDeleteExpiredConfirmationCode>().Should().NotBeNull();
            serviceProvider.GetRequiredService<ActionPruneNotifications>().Should().NotBeNull();
        }
    }
}
