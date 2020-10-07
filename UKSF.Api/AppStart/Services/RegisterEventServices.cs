using Microsoft.Extensions.DependencyInjection;
using UKSF.Api.Events;
using UKSF.Api.Events.Data;
using UKSF.Api.Events.Handlers;
using UKSF.Api.Events.SignalrServer;
using UKSF.Api.Interfaces.Events;
using UKSF.Api.Interfaces.Events.Handlers;
using UKSF.Api.Models.Admin;
using UKSF.Api.Models.Command;
using UKSF.Api.Models.Game;
using UKSF.Api.Models.Launcher;
using UKSF.Api.Models.Message;
using UKSF.Api.Models.Message.Logging;
using UKSF.Api.Models.Modpack;
using UKSF.Api.Models.Operations;
using UKSF.Api.Models.Personnel;
using UKSF.Api.Models.Units;
using UKSF.Api.Models.Utility;

namespace UKSF.Api.AppStart.Services {
    public static class EventServiceExtensions {
        public static void RegisterEventServices(this IServiceCollection services) {
            // Event Buses
            services.AddSingleton<IDataEventBus<Account>, DataEventBus<Account>>();
            services.AddSingleton<IDataEventBus<BasicLogMessage>, DataEventBus<BasicLogMessage>>();
            services.AddSingleton<IDataEventBus<CommandRequest>, DataEventBus<CommandRequest>>();
            services.AddSingleton<IDataEventBus<CommentThread>, DataEventBus<CommentThread>>();
            services.AddSingleton<IDataEventBus<ConfirmationCode>, DataEventBus<ConfirmationCode>>();
            services.AddSingleton<IDataEventBus<DischargeCollection>, DataEventBus<DischargeCollection>>();
            services.AddSingleton<IDataEventBus<GameServer>, DataEventBus<GameServer>>();
            services.AddSingleton<IDataEventBus<LauncherFile>, DataEventBus<LauncherFile>>();
            services.AddSingleton<IDataEventBus<Loa>, DataEventBus<Loa>>();
            services.AddSingleton<IDataEventBus<ModpackBuild>, DataEventBus<ModpackBuild>>();
            services.AddSingleton<IDataEventBus<ModpackRelease>, DataEventBus<ModpackRelease>>();
            services.AddSingleton<IDataEventBus<Notification>, DataEventBus<Notification>>();
            services.AddSingleton<IDataEventBus<Opord>, DataEventBus<Opord>>();
            services.AddSingleton<IDataEventBus<Oprep>, DataEventBus<Oprep>>();
            services.AddSingleton<IDataEventBus<Rank>, DataEventBus<Rank>>();
            services.AddSingleton<IDataEventBus<Role>, DataEventBus<Role>>();
            services.AddSingleton<IDataEventBus<ScheduledJob>, DataEventBus<ScheduledJob>>();
            services.AddSingleton<IDataEventBus<Unit>, DataEventBus<Unit>>();
            services.AddSingleton<IDataEventBus<VariableItem>, DataEventBus<VariableItem>>();
            services.AddSingleton<ISignalrEventBus, SignalrEventBus>();

            // Event Handlers
            services.AddSingleton<EventHandlerInitialiser>();
            services.AddSingleton<IAccountEventHandler, AccountEventHandler>();
            services.AddSingleton<IBuildsEventHandler, BuildsEventHandler>();
            services.AddSingleton<ICommandRequestEventHandler, CommandRequestEventHandler>();
            services.AddSingleton<ICommentThreadEventHandler, CommentThreadEventHandler>();
            services.AddSingleton<ILogEventHandler, LogEventHandler>();
            services.AddSingleton<INotificationsEventHandler, NotificationsEventHandler>();
            services.AddSingleton<ITeamspeakEventHandler, TeamspeakEventHandler>();
        }
    }
}
