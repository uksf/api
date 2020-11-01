using Microsoft.Extensions.DependencyInjection;
using UKSF.Api.Base.Events;
using UKSF.Api.Base.Models.Logging;
using UKSF.Api.Events.Handlers;
using UKSF.Api.Interfaces.Events.Handlers;
using UKSF.Api.Models.Command;
using UKSF.Api.Models.Game;
using UKSF.Api.Models.Launcher;
using UKSF.Api.Models.Modpack;
using UKSF.Api.Models.Operations;
using UKSF.Api.Personnel.EventHandlers;
using UKSF.Api.Personnel.Models;
using ISignalrEventBus = UKSF.Api.Interfaces.Events.ISignalrEventBus;

namespace UKSF.Api.AppStart.Services {
    public static class EventServiceExtensions {
        public static void RegisterEventServices(this IServiceCollection services) {
            // Event Buses
            services.AddSingleton<IDataEventBus<CommandRequest>, DataEventBus<CommandRequest>>();
            services.AddSingleton<IDataEventBus<GameServer>, DataEventBus<GameServer>>();
            services.AddSingleton<IDataEventBus<LauncherFile>, DataEventBus<LauncherFile>>();
            services.AddSingleton<IDataEventBus<ModpackBuild>, DataEventBus<ModpackBuild>>();
            services.AddSingleton<IDataEventBus<ModpackRelease>, DataEventBus<ModpackRelease>>();
            services.AddSingleton<IDataEventBus<Opord>, DataEventBus<Opord>>();
            services.AddSingleton<IDataEventBus<Oprep>, DataEventBus<Oprep>>();
            services.AddSingleton<ISignalrEventBus, SignalrEventBus>();

            services.AddSingleton<IDataEventBus<BasicLog>, DataEventBus<BasicLog>>();

            // Event Handlers
            services.AddSingleton<IBuildsEventHandler, BuildsEventHandler>();
            services.AddSingleton<ICommandRequestEventHandler, CommandRequestEventHandler>();
            services.AddSingleton<ITeamspeakEventHandler, TeamspeakEventHandler>();
        }
    }
}
