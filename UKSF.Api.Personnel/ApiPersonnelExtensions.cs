using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using UKSF.Api.Base.Events;
using UKSF.Api.Personnel.Context;
using UKSF.Api.Personnel.EventHandlers;
using UKSF.Api.Personnel.Models;
using UKSF.Api.Personnel.ScheduledActions;
using UKSF.Api.Personnel.Services;
using UKSF.Api.Personnel.Signalr.Hubs;
using UKSF.Api.Shared.Events;

namespace UKSF.Api.Personnel {
    public static class ApiPersonnelExtensions {
        public static IServiceCollection AddUksfPersonnel(this IServiceCollection services) =>
            services.AddContexts().AddEventBuses().AddEventHandlers().AddServices().AddActions().AddTransient<IActionDeleteExpiredConfirmationCode, ActionDeleteExpiredConfirmationCode>();

        private static IServiceCollection AddContexts(this IServiceCollection services) =>
            services.AddSingleton<IAccountDataService, AccountDataService>()
                    .AddSingleton<ICommentThreadDataService, CommentThreadDataService>()
                    .AddSingleton<INotificationsDataService, NotificationsDataService>()
                    .AddSingleton<IRanksDataService, RanksDataService>()
                    .AddSingleton<IRolesDataService, RolesDataService>()
                    .AddSingleton<IUnitsDataService, UnitsDataService>();

        private static IServiceCollection AddEventBuses(this IServiceCollection services) =>
            services.AddSingleton<IDataEventBus<Account>, DataEventBus<Account>>()
                    .AddSingleton<IDataEventBus<CommentThread>, DataEventBus<CommentThread>>()
                    .AddSingleton<IDataEventBus<ConfirmationCode>, DataEventBus<ConfirmationCode>>()
                    .AddSingleton<IDataEventBus<Loa>, DataEventBus<Loa>>()
                    .AddSingleton<IDataEventBus<Notification>, DataEventBus<Notification>>()
                    .AddSingleton<IDataEventBus<Rank>, DataEventBus<Rank>>()
                    .AddSingleton<IDataEventBus<Role>, DataEventBus<Role>>()
                    .AddSingleton<IDataEventBus<Unit>, DataEventBus<Unit>>()
                    .AddSingleton<IEventBus<Account>, EventBus<Account>>();

        private static IServiceCollection AddEventHandlers(this IServiceCollection services) =>
            services.AddSingleton<IAccountDataEventHandler, AccountDataEventHandler>()
                    .AddSingleton<ICommentThreadEventHandler, CommentThreadEventHandler>()
                    .AddSingleton<INotificationsEventHandler, NotificationsEventHandler>();

        private static IServiceCollection AddServices(this IServiceCollection services) =>
            services.AddSingleton<IAccountService, AccountService>()
                    .AddTransient<ICommentThreadService, CommentThreadService>()
                    .AddTransient<IConfirmationCodeService, ConfirmationCodeService>()
                    .AddTransient<INotificationsService, NotificationsService>()
                    .AddTransient<IObjectIdConversionService, ObjectIdConversionService>()
                    .AddTransient<IRanksService, RanksService>()
                    .AddTransient<IRecruitmentService, RecruitmentService>()
                    .AddTransient<IRolesService, RolesService>()
                    .AddTransient<IUnitsService, UnitsService>();

        private static IServiceCollection AddActions(this IServiceCollection services) =>
            services.AddSingleton<IActionDeleteExpiredConfirmationCode, ActionDeleteExpiredConfirmationCode>().AddSingleton<IActionPruneNotifications, ActionPruneNotifications>();

        public static void AddUksfPersonnelSignalr(this IEndpointRouteBuilder builder) {
            builder.MapHub<AccountHub>($"/hub/{AccountHub.END_POINT}");
            builder.MapHub<CommentThreadHub>($"/hub/{CommentThreadHub.END_POINT}");
            builder.MapHub<NotificationHub>($"/hub/{NotificationHub.END_POINT}");
        }
    }
}
