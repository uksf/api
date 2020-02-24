using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using UKSF.Api.Data;
using UKSF.Api.Interfaces.Command;
using UKSF.Api.Interfaces.Data;
using UKSF.Api.Interfaces.Game;
using UKSF.Api.Interfaces.Integrations;
using UKSF.Api.Interfaces.Integrations.Teamspeak;
using UKSF.Api.Interfaces.Launcher;
using UKSF.Api.Interfaces.Message;
using UKSF.Api.Interfaces.Personnel;
using UKSF.Api.Interfaces.Utility;
using UKSF.Api.Services.Admin;
using UKSF.Api.Services.Command;
using UKSF.Api.Services.Fake;
using UKSF.Api.Services.Game;
using UKSF.Api.Services.Game.Missions;
using UKSF.Api.Services.Integrations;
using UKSF.Api.Services.Integrations.Teamspeak;
using UKSF.Api.Services.Launcher;
using UKSF.Api.Services.Message;
using UKSF.Api.Services.Personnel;
using UKSF.Api.Services.Utility;

namespace UKSF.Api.AppStart {
    public static class ServiceExtensions {
        public static void RegisterServices(this IServiceCollection services, IConfiguration configuration, IHostEnvironment currentEnvironment) {
            services.AddSingleton(configuration);
            services.AddSingleton(currentEnvironment);

            services.AddSingleton(MongoClientFactory.GetDatabase(configuration.GetConnectionString("database")));
            services.AddTransient<IDataCollectionFactory, DataCollectionFactory>();
            services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
            services.AddSingleton<DataCacheService>();

            services.RegisterEventServices();
            services.RegisterDataServices(currentEnvironment);
            services.RegisterDataBackedServices(currentEnvironment);

            // Services
            services.AddTransient<IAssignmentService, AssignmentService>();
            services.AddTransient<IAttendanceService, AttendanceService>();
            services.AddTransient<IChainOfCommandService, ChainOfCommandService>();
            services.AddTransient<ICommandRequestCompletionService, CommandRequestCompletionService>();
            services.AddTransient<IDisplayNameService, DisplayNameService>();
            services.AddTransient<ILauncherService, LauncherService>();
            services.AddTransient<ILoginService, LoginService>();
            services.AddTransient<IMissionPatchingService, MissionPatchingService>();
            services.AddTransient<IRecruitmentService, RecruitmentService>();
            services.AddTransient<IServerService, ServerService>();
            services.AddTransient<IServiceRecordService, ServiceRecordService>();
            services.AddTransient<ITeamspeakGroupService, TeamspeakGroupService>();
            services.AddTransient<ITeamspeakMetricsService, TeamspeakMetricsService>();
            services.AddTransient<MissionPatchDataService>();
            services.AddTransient<MissionService>();

            services.AddSingleton<MigrationUtility>();
            services.AddSingleton<IEmailService, EmailService>();
            services.AddSingleton<ISessionService, SessionService>();
            services.AddSingleton<ITeamspeakService, TeamspeakService>();

            if (currentEnvironment.IsDevelopment()) {
                services.AddSingleton<ITeamspeakManagerService, FakeTeamspeakManagerService>();
                services.AddSingleton<IDiscordService, FakeDiscordService>();
            } else {
                services.AddSingleton<ITeamspeakManagerService, TeamspeakManagerService>();
                services.AddSingleton<IDiscordService, DiscordService>();
            }
        }
    }
}
