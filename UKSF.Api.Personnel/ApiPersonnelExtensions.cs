using Microsoft.Extensions.DependencyInjection;
using UKSF.Api.Base.Events;
using UKSF.Api.Personnel.EventHandlers;
using UKSF.Api.Personnel.Models;
using UKSF.Api.Personnel.ScheduledActions;
using UKSF.Api.Personnel.Services;
using UKSF.Api.Personnel.Services.Data;

namespace UKSF.Api.Personnel {
    public static class ApiPersonnelExtensions {
        public static IServiceCollection AddUksfPersonnel(this IServiceCollection services) {
            services.AddSingleton<IAccountDataService, AccountDataService>();
            services.AddSingleton<ICommentThreadDataService, CommentThreadDataService>();
            services.AddSingleton<IDischargeDataService, DischargeDataService>();
            services.AddSingleton<ILoaDataService, LoaDataService>();
            services.AddSingleton<INotificationsDataService, NotificationsDataService>();
            services.AddSingleton<IRanksDataService, RanksDataService>();
            services.AddSingleton<IRolesDataService, RolesDataService>();
            services.AddSingleton<IUnitsDataService, UnitsDataService>();

            services.AddSingleton<IAccountService, AccountService>();
            services.AddTransient<ICommentThreadService, CommentThreadService>();
            services.AddTransient<IConfirmationCodeService, ConfirmationCodeService>();
            services.AddTransient<IDischargeService, DischargeService>();
            services.AddTransient<ILoaService, LoaService>();
            services.AddTransient<INotificationsService, NotificationsService>();
            services.AddTransient<IObjectIdConversionService, ObjectIdConversionService>();
            services.AddTransient<IRanksService, RanksService>();
            services.AddTransient<IRolesService, RolesService>();
            services.AddTransient<IUnitsService, UnitsService>();

            services.AddSingleton<IDataEventBus<Account>, DataEventBus<Account>>();
            services.AddSingleton<IDataEventBus<CommentThread>, DataEventBus<CommentThread>>();
            services.AddSingleton<IDataEventBus<ConfirmationCode>, DataEventBus<ConfirmationCode>>();
            services.AddSingleton<IDataEventBus<DischargeCollection>, DataEventBus<DischargeCollection>>();
            services.AddSingleton<IDataEventBus<Loa>, DataEventBus<Loa>>();
            services.AddSingleton<IDataEventBus<Notification>, DataEventBus<Notification>>();
            services.AddSingleton<IDataEventBus<Rank>, DataEventBus<Rank>>();
            services.AddSingleton<IDataEventBus<Role>, DataEventBus<Role>>();
            services.AddSingleton<IDataEventBus<Unit>, DataEventBus<Unit>>();

            services.AddSingleton<IAccountEventHandler, AccountEventHandler>();
            services.AddSingleton<ICommentThreadEventHandler, CommentThreadEventHandler>();
            services.AddSingleton<INotificationsEventHandler, NotificationsEventHandler>();

            // services.AddTransient<IAttendanceService, AttendanceService>();
            services.AddTransient<IRecruitmentService, RecruitmentService>();

            services.AddTransient<IDeleteExpiredConfirmationCodeAction, DeleteExpiredConfirmationCodeAction>();

            return services;
        }
    }
}
