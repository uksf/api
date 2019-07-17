using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Swashbuckle.AspNetCore.Swagger;
using UKSFWebsite.Api.Services.Abstraction;
using UKSFWebsite.Api.Services.Data;
using UKSFWebsite.Api.Services.Utility;

namespace UKSFWebsite.Steam {
    public class Startup {
        private readonly IConfiguration configuration;

        public Startup(IHostingEnvironment currentEnvironment, IConfiguration configuration) {
            this.configuration = configuration;
            IConfigurationBuilder builder = new ConfigurationBuilder().SetBasePath(currentEnvironment.ContentRootPath).AddEnvironmentVariables();
            builder.Build();
        }

        public void ConfigureServices(IServiceCollection services) {
            services.RegisterServices(configuration);
            services.BuildServiceProvider();

            services.AddSwaggerGen(c => { c.SwaggerDoc("v1", new Info {Title = "Server", Version = "v1"}); });
            services.AddCors();
            services.AddAuthentication(options => { options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme; }).AddCookie().AddSteam();

            services.AddMvc();
        }

        public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory) {
            app.UseCors(options => options.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod().AllowCredentials());
            app.UseAuthentication();
            app.UseSwagger();
            app.UseSwaggerUI(c => { c.SwaggerEndpoint("/swagger/v1/swagger.json", "V1 Docs"); });
            app.UseMvc();
            app.UseHsts();
            app.UseHttpsRedirection();

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
            services.AddSingleton<IVariablesService, VariablesService>();

            return services;
        }
    }
}
