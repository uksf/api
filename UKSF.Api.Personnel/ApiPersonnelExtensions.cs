using AutoMapper;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using UKSF.Api.Personnel.Context;
using UKSF.Api.Personnel.EventHandlers;
using UKSF.Api.Personnel.Mappers;
using UKSF.Api.Personnel.ScheduledActions;
using UKSF.Api.Personnel.Services;
using UKSF.Api.Personnel.Signalr.Hubs;

namespace UKSF.Api.Personnel {
    public static class ApiPersonnelExtensions {
        public static IServiceCollection AddUksfPersonnel(this IServiceCollection services) =>
            services.AddContexts()
                    .AddEventHandlers()
                    .AddServices()
                    .AddActions()
                    .AddTransient<IActionDeleteExpiredConfirmationCode, ActionDeleteExpiredConfirmationCode>()
                    .AddAutoMapper(typeof(AutoMapperUnitProfile));

        private static IServiceCollection AddContexts(this IServiceCollection services) =>
            services.AddSingleton<IAccountContext, AccountContext>()
                    .AddSingleton<ICommentThreadContext, CommentThreadContext>()
                    .AddSingleton<IConfirmationCodeContext, ConfirmationCodeContext>()
                    .AddSingleton<INotificationsContext, NotificationsContext>()
                    .AddSingleton<IRanksContext, RanksContext>()
                    .AddSingleton<IRolesContext, RolesContext>()
                    .AddSingleton<IUnitsContext, UnitsContext>();

        private static IServiceCollection AddEventHandlers(this IServiceCollection services) =>
            services.AddSingleton<IAccountDataEventHandler, AccountDataEventHandler>()
                    .AddSingleton<ICommentThreadEventHandler, CommentThreadEventHandler>()
                    .AddSingleton<IDiscordEventhandler, DiscordEventhandler>()
                    .AddSingleton<INotificationsEventHandler, NotificationsEventHandler>();

        private static IServiceCollection AddServices(this IServiceCollection services) =>
            services.AddSingleton<IAccountService, AccountService>()
                    .AddSingleton<IAssignmentService, AssignmentService>()
                    .AddSingleton<ICommentThreadService, CommentThreadService>()
                    .AddSingleton<IConfirmationCodeService, ConfirmationCodeService>()
                    .AddSingleton<IDisplayNameService, DisplayNameService>()
                    .AddSingleton<IEmailService, EmailService>()
                    .AddSingleton<INotificationsService, NotificationsService>()
                    .AddSingleton<IObjectIdConversionService, ObjectIdConversionService>()
                    .AddSingleton<IRanksService, RanksService>()
                    .AddSingleton<IRecruitmentService, RecruitmentService>()
                    .AddSingleton<IRolesService, RolesService>()
                    .AddSingleton<IServiceRecordService, ServiceRecordService>()
                    .AddSingleton<IUnitsService, UnitsService>();

        private static IServiceCollection AddActions(this IServiceCollection services) =>
            services.AddSingleton<IActionDeleteExpiredConfirmationCode, ActionDeleteExpiredConfirmationCode>().AddSingleton<IActionPruneNotifications, ActionPruneNotifications>();

        public static void AddUksfPersonnelSignalr(this IEndpointRouteBuilder builder) {
            builder.MapHub<AccountHub>($"/hub/{AccountHub.END_POINT}");
            builder.MapHub<CommentThreadHub>($"/hub/{CommentThreadHub.END_POINT}");
            builder.MapHub<NotificationHub>($"/hub/{NotificationHub.END_POINT}");
        }
    }
}
