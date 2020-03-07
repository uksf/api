using Microsoft.Extensions.DependencyInjection;
using UKSF.Api.Events;
using UKSF.Api.Events.Data;
using UKSF.Api.Events.Handlers;
using UKSF.Api.Events.SignalrServer;
using UKSF.Api.Interfaces.Data;
using UKSF.Api.Interfaces.Data.Cached;
using UKSF.Api.Interfaces.Events;
using UKSF.Api.Interfaces.Events.Handlers;

namespace UKSF.Api.AppStart.Services {
    public static class EventServiceExtensions {
        public static void RegisterEventServices(this IServiceCollection services) {
            // Event Buses
            services.AddSingleton<IDataEventBus<IAccountDataService>, DataEventBus<IAccountDataService>>();
            services.AddSingleton<IDataEventBus<ICommandRequestDataService>, DataEventBus<ICommandRequestDataService>>();
            services.AddSingleton<IDataEventBus<ICommandRequestArchiveDataService>, DataEventBus<ICommandRequestArchiveDataService>>();
            services.AddSingleton<IDataEventBus<ICommentThreadDataService>, DataEventBus<ICommentThreadDataService>>();
            services.AddSingleton<IDataEventBus<IConfirmationCodeDataService>, DataEventBus<IConfirmationCodeDataService>>();
            services.AddSingleton<IDataEventBus<IDischargeDataService>, DataEventBus<IDischargeDataService>>();
            services.AddSingleton<IDataEventBus<IGameServersDataService>, DataEventBus<IGameServersDataService>>();
            services.AddSingleton<IDataEventBus<ILauncherFileDataService>, DataEventBus<ILauncherFileDataService>>();
            services.AddSingleton<IDataEventBus<ILoaDataService>, DataEventBus<ILoaDataService>>();
            services.AddSingleton<IDataEventBus<ILogDataService>, DataEventBus<ILogDataService>>();
            services.AddSingleton<IDataEventBus<INotificationsDataService>, DataEventBus<INotificationsDataService>>();
            services.AddSingleton<IDataEventBus<IOperationOrderDataService>, DataEventBus<IOperationOrderDataService>>();
            services.AddSingleton<IDataEventBus<IOperationReportDataService>, DataEventBus<IOperationReportDataService>>();
            services.AddSingleton<IDataEventBus<ISchedulerDataService>, DataEventBus<ISchedulerDataService>>();
            services.AddSingleton<IDataEventBus<IRanksDataService>, DataEventBus<IRanksDataService>>();
            services.AddSingleton<IDataEventBus<IRolesDataService>, DataEventBus<IRolesDataService>>();
            services.AddSingleton<IDataEventBus<IUnitsDataService>, DataEventBus<IUnitsDataService>>();
            services.AddSingleton<IDataEventBus<IVariablesDataService>, DataEventBus<IVariablesDataService>>();
            services.AddSingleton<ISignalrEventBus, SignalrEventBus>();

            // Event Handlers
            services.AddSingleton<EventHandlerInitialiser>();
            services.AddSingleton<IAccountEventHandler, AccountEventHandler>();
            services.AddSingleton<ICommandRequestEventHandler, CommandRequestEventHandler>();
            services.AddSingleton<ICommentThreadEventHandler, CommentThreadEventHandler>();
            services.AddSingleton<ILogEventHandler, LogEventHandler>();
            services.AddSingleton<INotificationsEventHandler, NotificationsEventHandler>();
            services.AddSingleton<ITeamspeakEventHandler, TeamspeakEventHandler>();
        }
    }
}
