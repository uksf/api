using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
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
using UKSF.Api.Services;
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

            ExceptionHandler exceptionHandler = null;
            services.AddSingleton(
                provider => {
                    exceptionHandler = new ExceptionHandler(provider.GetService<ISessionService>(), provider.GetService<IDisplayNameService>());
                    return exceptionHandler;
                }
            );
            if (exceptionHandler == null) throw new NullReferenceException("Could not create ExceptionHandler");

            services.AddCors(
                options => options.AddPolicy(
                    "CorsPolicy",
                    builder => {
                        builder.AllowAnyMethod().AllowAnyHeader().WithOrigins("http://localhost:4200", "http://localhost:5100", "https://uk-sf.co.uk", "https://api.uk-sf.co.uk").AllowCredentials();
                    }
                )
            );
            services.AddAuthentication(options => { options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme; }).AddCookie().AddSteam();

            services.AddControllers();
            services.AddSwaggerGen(options => { options.SwaggerDoc("v1", new OpenApiInfo { Title = "UKSF Integrations API", Version = "v1" }); });
            services.AddMvc(options => { options.Filters.Add(exceptionHandler); }).AddNewtonsoftJson();
        }

        // ReSharper disable once UnusedMember.Global
        public void Configure(IApplicationBuilder app) {
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
            app.UseForwardedHeaders(new ForwardedHeadersOptions { ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto });
            app.UseEndpoints(endpoints => { endpoints.MapControllers().RequireCors("CorsPolicy"); });

            Global.ServiceProvider = app.ApplicationServices;
            ServiceWrapper.ServiceProvider = Global.ServiceProvider;

            // Warm cached data services
            RegisterAndWarmCachedData();

            // Start scheduler
            Global.ServiceProvider.GetService<ISchedulerService>().LoadIntegrations();
        }

        private static void RegisterAndWarmCachedData() {
            IServiceProvider serviceProvider = Global.ServiceProvider;
            IAccountDataService accountDataService = serviceProvider.GetService<IAccountDataService>();
            IRanksDataService ranksDataService = serviceProvider.GetService<IRanksDataService>();
            IVariablesDataService variablesDataService = serviceProvider.GetService<IVariablesDataService>();

            DataCacheService dataCacheService = serviceProvider.GetService<DataCacheService>();
            dataCacheService.RegisterCachedDataServices(new List<ICachedDataService> { accountDataService, ranksDataService, variablesDataService });
            dataCacheService.InvalidateCachedData();
        }
    }

    public static class ServiceExtensions {
        public static void RegisterServices(this IServiceCollection services, IConfiguration configuration) {
            // Base
            services.AddSingleton(configuration);
            services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();

            // Database
            services.AddSingleton(MongoClientFactory.GetDatabase(configuration.GetConnectionString("database")));
            services.AddTransient<IDataCollectionFactory, DataCollectionFactory>();
            services.AddSingleton<DataCacheService>();

            // Event Buses
            services.AddSingleton<IDataEventBus<IAccountDataService>, DataEventBus<IAccountDataService>>();
            services.AddSingleton<IDataEventBus<IConfirmationCodeDataService>, DataEventBus<IConfirmationCodeDataService>>();
            services.AddSingleton<IDataEventBus<ILogDataService>, DataEventBus<ILogDataService>>();
            services.AddSingleton<IDataEventBus<IRanksDataService>, DataEventBus<IRanksDataService>>();
            services.AddSingleton<IDataEventBus<ISchedulerDataService>, DataEventBus<ISchedulerDataService>>();
            services.AddSingleton<IDataEventBus<IVariablesDataService>, DataEventBus<IVariablesDataService>>();

            // Data
            services.AddSingleton<IAccountDataService, AccountDataService>();
            services.AddSingleton<IConfirmationCodeDataService, ConfirmationCodeDataService>();
            services.AddSingleton<ILogDataService, LogDataService>();
            services.AddSingleton<IRanksDataService, RanksDataService>();
            services.AddSingleton<ISchedulerDataService, SchedulerIntegrationsDataService>();
            services.AddSingleton<IVariablesDataService, VariablesDataService>();

            // Services
            services.AddTransient<IConfirmationCodeService, ConfirmationCodeService>();
            services.AddTransient<IDisplayNameService, DisplayNameService>();
            services.AddTransient<IRanksService, RanksService>();
            services.AddTransient<ISchedulerService, SchedulerService>();

            services.AddSingleton<IAccountService, AccountService>();
            services.AddSingleton<ILoggingService, LoggingService>();
            services.AddSingleton<ISessionService, SessionService>();
        }
    }
}
