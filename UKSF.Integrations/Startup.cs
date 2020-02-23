using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using UKSF.Api.Data;
using UKSF.Api.Data.Admin;
using UKSF.Api.Data.Message;
using UKSF.Api.Data.Personnel;
using UKSF.Api.Data.Utility;
using UKSF.Api.Events.Data;
using UKSF.Api.Interfaces.Data;
using UKSF.Api.Interfaces.Data.Cached;
using UKSF.Api.Interfaces.Events;
using UKSF.Api.Interfaces.Message;
using UKSF.Api.Interfaces.Personnel;
using UKSF.Api.Interfaces.Utility;
using UKSF.Api.Services.Common;
using UKSF.Api.Services.Message;
using UKSF.Api.Services.Personnel;
using UKSF.Api.Services.Utility;

namespace UKSF.Integrations {
    public class Startup {
        private readonly IConfiguration configuration;

        public Startup(IHostEnvironment currentEnvironment, IConfiguration configuration) {
            this.configuration = configuration;
            IConfigurationBuilder builder = new ConfigurationBuilder().SetBasePath(currentEnvironment.ContentRootPath).AddEnvironmentVariables();
            builder.Build();
        }

        public void ConfigureServices(IServiceCollection services) {
            services.RegisterServices(configuration);

            services.AddCors(options => options.AddPolicy("CorsPolicy", builder => { builder.AllowAnyMethod().AllowAnyHeader().WithOrigins("http://localhost:4200", "http://localhost:5100", "https://uk-sf.co.uk", "https://api.uk-sf.co.uk").AllowCredentials(); }));
            services.AddAuthentication(options => { options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme; }).AddCookie().AddSteam();

            services.AddControllers();
            services.AddSwaggerGen(options => { options.SwaggerDoc("v1", new OpenApiInfo {Title = "UKSF Integrations API", Version = "v1"}); });
            services.AddMvc().AddNewtonsoftJson();
        }

        public void Configure(IApplicationBuilder app, IHostEnvironment env, ILoggerFactory loggerFactory) {
            if (env.IsDevelopment()) {
                app.UseDeveloperExceptionPage();
            }

            app.UseCookiePolicy(new CookiePolicyOptions {MinimumSameSitePolicy = SameSiteMode.Lax});
            app.UseSwagger();
            app.UseSwaggerUI(options => { options.SwaggerEndpoint("/swagger/v1/swagger.json", "UKSF Integrations API v1"); });
            app.UseRouting();
            app.UseCors("CorsPolicy");
            app.UseAuthentication();
            app.UseAuthorization();
            app.UseHsts();
            app.UseForwardedHeaders(new ForwardedHeadersOptions {ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto});
            app.UseEndpoints(endpoints => { endpoints.MapControllers().RequireCors("CorsPolicy"); });

            Global.ServiceProvider = app.ApplicationServices;
            ServiceWrapper.ServiceProvider = Global.ServiceProvider;

            // Start scheduler
            Global.ServiceProvider.GetService<ISchedulerService>().Load(false);
        }
    }

    public static class ServiceExtensions {
        public static void RegisterServices(this IServiceCollection services, IConfiguration configuration) {
            // Instance Objects
            services.AddTransient<IConfirmationCodeService, ConfirmationCodeService>();
            services.AddTransient<ISchedulerService, SchedulerService>();

            // Global Singletons
            services.AddSingleton(configuration);
            services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
            services.AddSingleton<IAccountService, AccountService>();
            services.AddTransient<IDisplayNameService, DisplayNameService>();
            services.AddSingleton<ILoggingService, LoggingService>();
            services.AddTransient<IRanksService, RanksService>();

            services.AddSingleton(_ => MongoClientFactory.GetDatabase(configuration.GetConnectionString("database")));
            services.AddTransient<IDataCollectionFactory, DataCollectionFactory>();
            services.AddSingleton<IAccountDataService, AccountDataService>();
            services.AddSingleton<IConfirmationCodeDataService, ConfirmationCodeDataService>();
            services.AddSingleton<ILogDataService, LogDataService>();
            services.AddSingleton<IRanksDataService, RanksDataService>();
            services.AddSingleton<ISchedulerDataService, SchedulerIntegrationsDataService>();
            services.AddSingleton<IVariablesDataService, VariablesDataService>();

            // Event Buses
            services.AddSingleton<IDataEventBus<IAccountDataService>, DataEventBus<IAccountDataService>>();
            services.AddSingleton<IDataEventBus<IConfirmationCodeDataService>, DataEventBus<IConfirmationCodeDataService>>();
            services.AddSingleton<IDataEventBus<ILogDataService>, DataEventBus<ILogDataService>>();
            services.AddSingleton<IDataEventBus<IRanksDataService>, DataEventBus<IRanksDataService>>();
            services.AddSingleton<IDataEventBus<ISchedulerDataService>, DataEventBus<ISchedulerDataService>>();
            services.AddSingleton<IDataEventBus<IVariablesDataService>, DataEventBus<IVariablesDataService>>();
        }
    }
}
