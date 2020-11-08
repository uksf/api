using Microsoft.Extensions.DependencyInjection;
using UKSF.Api.Base.Events;
using UKSF.Api.Personnel.EventHandlers;
using UKSF.Api.Personnel.Models;
using UKSF.Api.Personnel.ScheduledActions;
using UKSF.Api.Personnel.Services;
using UKSF.Api.Personnel.Services.Data;

namespace UKSF.Api.Personnel {
    public static class ApiPersonnelExtensions {
        public static IServiceCollection AddUksfPersonnel(this IServiceCollection services) =>
            services.AddContexts().AddEventBuses().AddEventHandlers().AddServices().AddTransient<IDeleteExpiredConfirmationCodeAction, DeleteExpiredConfirmationCodeAction>();

        private static IServiceCollection AddContexts(this IServiceCollection services) =>
            services.AddSingleton<IAccountDataService, AccountDataService>()
                    .AddSingleton<ICommentThreadDataService, CommentThreadDataService>()
                    .AddSingleton<IDischargeDataService, DischargeDataService>()
                    .AddSingleton<ILoaDataService, LoaDataService>()
                    .AddSingleton<INotificationsDataService, NotificationsDataService>()
                    .AddSingleton<IRanksDataService, RanksDataService>()
                    .AddSingleton<IRolesDataService, RolesDataService>()
                    .AddSingleton<IUnitsDataService, UnitsDataService>();

        private static IServiceCollection AddEventBuses(this IServiceCollection services) =>
            services.AddSingleton<IDataEventBus<Account>, DataEventBus<Account>>()
                    .AddSingleton<IDataEventBus<CommentThread>, DataEventBus<CommentThread>>()
                    .AddSingleton<IDataEventBus<ConfirmationCode>, DataEventBus<ConfirmationCode>>()
                    .AddSingleton<IDataEventBus<DischargeCollection>, DataEventBus<DischargeCollection>>()
                    .AddSingleton<IDataEventBus<Loa>, DataEventBus<Loa>>()
                    .AddSingleton<IDataEventBus<Notification>, DataEventBus<Notification>>()
                    .AddSingleton<IDataEventBus<Rank>, DataEventBus<Rank>>()
                    .AddSingleton<IDataEventBus<Role>, DataEventBus<Role>>()
                    .AddSingleton<IDataEventBus<Unit>, DataEventBus<Unit>>();

        private static IServiceCollection AddEventHandlers(this IServiceCollection services) =>
            services.AddSingleton<IAccountEventHandler, AccountEventHandler>()
                    .AddSingleton<ICommentThreadEventHandler, CommentThreadEventHandler>()
                    .AddSingleton<INotificationsEventHandler, NotificationsEventHandler>();

        private static IServiceCollection AddServices(this IServiceCollection services) =>
            services.AddSingleton<IAccountService, AccountService>()
                    .AddTransient<ICommentThreadService, CommentThreadService>()
                    .AddTransient<IConfirmationCodeService, ConfirmationCodeService>()
                    .AddTransient<IDischargeService, DischargeService>()
                    .AddTransient<ILoaService, LoaService>()
                    .AddTransient<INotificationsService, NotificationsService>()
                    .AddTransient<IObjectIdConversionService, ObjectIdConversionService>()
                    .AddTransient<IRanksService, RanksService>()
                    .AddTransient<IRecruitmentService, RecruitmentService>()
                    .AddTransient<IRolesService, RolesService>()
                    .AddTransient<IUnitsService, UnitsService>();
    }
}
