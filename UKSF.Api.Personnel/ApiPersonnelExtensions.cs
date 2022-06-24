using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using UKSF.Api.Personnel.Commands;
using UKSF.Api.Personnel.Context;
using UKSF.Api.Personnel.EventHandlers;
using UKSF.Api.Personnel.Mappers;
using UKSF.Api.Personnel.Queries;
using UKSF.Api.Personnel.ScheduledActions;
using UKSF.Api.Personnel.Services;
using UKSF.Api.Personnel.Signalr.Hubs;

namespace UKSF.Api.Personnel
{
    public static class ApiPersonnelExtensions
    {
        public static IServiceCollection AddUksfPersonnel(this IServiceCollection services)
        {
            return services.AddContexts()
                           .AddEventHandlers()
                           .AddServices()
                           .AddCommands()
                           .AddQueries()
                           .AddMappers()
                           .AddActions()
                           .AddTransient<IActionDeleteExpiredConfirmationCode, ActionDeleteExpiredConfirmationCode>()
                           .AddAutoMapper(typeof(AutoMapperUnitProfile));
        }

        private static IServiceCollection AddContexts(this IServiceCollection services)
        {
            return services.AddSingleton<IAccountContext, AccountContext>()
                           .AddSingleton<ICommentThreadContext, CommentThreadContext>()
                           .AddSingleton<IConfirmationCodeContext, ConfirmationCodeContext>()
                           .AddSingleton<INotificationsContext, NotificationsContext>()
                           .AddSingleton<IRanksContext, RanksContext>()
                           .AddSingleton<IRolesContext, RolesContext>()
                           .AddSingleton<IUnitsContext, UnitsContext>();
        }

        private static IServiceCollection AddEventHandlers(this IServiceCollection services)
        {
            return services.AddSingleton<IAccountDataEventHandler, AccountDataEventHandler>()
                           .AddSingleton<ICommentThreadEventHandler, CommentThreadEventHandler>()
                           .AddSingleton<IDiscordEventhandler, DiscordEventhandler>()
                           .AddSingleton<INotificationsEventHandler, NotificationsEventHandler>();
        }

        private static IServiceCollection AddServices(this IServiceCollection services)
        {
            return services.AddSingleton<IAccountService, AccountService>()
                           .AddSingleton<IAssignmentService, AssignmentService>()
                           .AddSingleton<ICommentThreadService, CommentThreadService>()
                           .AddSingleton<IConfirmationCodeService, ConfirmationCodeService>()
                           .AddSingleton<IDisplayNameService, DisplayNameService>()
                           .AddSingleton<INotificationsService, NotificationsService>()
                           .AddSingleton<IObjectIdConversionService, ObjectIdConversionService>()
                           .AddSingleton<IRanksService, RanksService>()
                           .AddSingleton<IRecruitmentService, RecruitmentService>()
                           .AddSingleton<IRolesService, RolesService>()
                           .AddSingleton<IServiceRecordService, ServiceRecordService>()
                           .AddSingleton<IUnitsService, UnitsService>();
        }

        private static IServiceCollection AddCommands(this IServiceCollection services)
        {
            return services.AddSingleton<IConnectTeamspeakIdToAccountCommand, ConnectTeamspeakIdToAccountCommand>()
                           .AddSingleton<ICreateApplicationCommand, CreateApplicationCommand>()
                           .AddSingleton<ICreateCommentThreadCommand, CreateCommentThreadCommand>()
                           .AddSingleton<IQualificationsUpdateCommand, QualificationsUpdateCommand>();
        }

        private static IServiceCollection AddQueries(this IServiceCollection services)
        {
            return services.AddSingleton<IAllNationsByAccountQuery, AllNationsByAccountQuery>().AddSingleton<IGetUnitTreeQuery, GetUnitTreeQuery>();
        }

        private static IServiceCollection AddMappers(this IServiceCollection services)
        {
            return services.AddSingleton<IAccountMapper, AccountMapper>().AddSingleton<IUnitTreeMapper, UnitTreeMapper>();
        }

        private static IServiceCollection AddActions(this IServiceCollection services)
        {
            return services.AddSingleton<IActionDeleteExpiredConfirmationCode, ActionDeleteExpiredConfirmationCode>()
                           .AddSingleton<IActionPruneNotifications, ActionPruneNotifications>();
        }

        public static void AddUksfPersonnelSignalr(this IEndpointRouteBuilder builder)
        {
            builder.MapHub<AccountHub>($"/hub/{AccountHub.EndPoint}");
            builder.MapHub<CommentThreadHub>($"/hub/{CommentThreadHub.EndPoint}");
            builder.MapHub<NotificationHub>($"/hub/{NotificationHub.EndPoint}");
        }
    }
}
