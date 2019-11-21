using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using UKSFWebsite.Api.Data.Admin;
using UKSFWebsite.Api.Data.Utility;
using UKSFWebsite.Api.Events.Data;
using UKSFWebsite.Api.Interfaces.Data;
using UKSFWebsite.Api.Interfaces.Data.Cached;
using UKSFWebsite.Api.Interfaces.Events;
using UKSFWebsite.Api.Interfaces.Utility;
using UKSFWebsite.Api.Services;
using UKSFWebsite.Api.Services.Utility;

namespace UKSFWebsite.Integrations {
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
            services.AddMvc().AddNewtonsoftJson();
        }

        public void Configure(IApplicationBuilder app, IHostEnvironment env, ILoggerFactory loggerFactory) {
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
            Global.ServiceProvider.GetService<ISchedulerService>().Load(true);
        }
    }

    public static class ServiceExtensions {
        public static IServiceCollection RegisterServices(this IServiceCollection services, IConfiguration configuration) {
            // Instance Objects
            services.AddTransient<IConfirmationCodeService, ConfirmationCodeService>();
            services.AddTransient<ISchedulerService, SchedulerService>();

            // Global Singletons
            services.AddSingleton(configuration);
            services.AddSingleton(_ => MongoClientFactory.GetDatabase(configuration.GetConnectionString("database")));
            services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
            services.AddSingleton<IConfirmationCodeDataService, ConfirmationCodeDataService>();
            services.AddSingleton<ISchedulerDataService, SchedulerDataService>();
            services.AddSingleton<IVariablesDataService, VariablesDataService>();
            
            // Event Buses
            services.AddSingleton<IDataEventBus<IConfirmationCodeDataService>, DataEventBus<IConfirmationCodeDataService>>();
            services.AddSingleton<IDataEventBus<ISchedulerDataService>, DataEventBus<ISchedulerDataService>>();
            services.AddSingleton<IDataEventBus<IVariablesDataService>, DataEventBus<IVariablesDataService>>();

            return services;
        }
    }
}
