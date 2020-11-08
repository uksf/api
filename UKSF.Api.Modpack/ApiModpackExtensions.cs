using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using UKSF.Api.Base.Events;
using UKSF.Api.Modpack.EventHandlers;
using UKSF.Api.Modpack.Models;
using UKSF.Api.Modpack.Services;
using UKSF.Api.Modpack.Services.BuildProcess;
using UKSF.Api.Modpack.Services.Data;
using UKSF.Api.Modpack.Signalr.Hubs;

namespace UKSF.Api.Modpack {
    public static class ApiModpackExtensions {
        public static IServiceCollection AddUksfModpack(this IServiceCollection services) =>
            services.AddContexts().AddEventBuses().AddEventHandlers().AddServices().AddTransient<IBuildsEventHandler, BuildsEventHandler>();

        private static IServiceCollection AddContexts(this IServiceCollection services) =>
            services.AddSingleton<IBuildsDataService, BuildsDataService>().AddSingleton<IReleasesDataService, ReleasesDataService>();

        private static IServiceCollection AddEventBuses(this IServiceCollection services) =>
            services.AddSingleton<IDataEventBus<ModpackBuild>, DataEventBus<ModpackBuild>>().AddSingleton<IDataEventBus<ModpackRelease>, DataEventBus<ModpackRelease>>();

        private static IServiceCollection AddEventHandlers(this IServiceCollection services) => services.AddSingleton<IBuildsEventHandler, BuildsEventHandler>();

        private static IServiceCollection AddServices(this IServiceCollection services) =>
            services.AddSingleton<IBuildsService, BuildsService>()
                    .AddTransient<IGithubService, GithubService>()
                    .AddTransient<IModpackService, ModpackService>()
                    .AddTransient<IReleaseService, ReleaseService>()
                    .AddTransient<IBuildStepService, BuildStepService>()
                    .AddTransient<IBuildQueueService, BuildQueueService>();

        public static void AddUksfModpackSignalr(this IEndpointRouteBuilder builder) {
            builder.MapHub<BuildsHub>($"/hub/{BuildsHub.END_POINT}");
        }
    }
}
