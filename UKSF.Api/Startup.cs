using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json.Serialization;
using Swashbuckle.AspNetCore.SwaggerUI;
using UKSF.Api.Admin;
using UKSF.Api.AppStart;
using UKSF.Api.ArmaServer;
using UKSF.Api.Command;
using UKSF.Api.Middleware;
using UKSF.Api.Modpack;
using UKSF.Api.Personnel;
using UKSF.Api.Teamspeak;

namespace UKSF.Api
{
    public class Startup
    {
        private readonly IConfiguration _configuration;
        private readonly IHostEnvironment _currentEnvironment;

        public Startup(IHostEnvironment currentEnvironment, IConfiguration configuration)
        {
            _configuration = configuration;
            _currentEnvironment = currentEnvironment;
            IConfigurationBuilder builder = new ConfigurationBuilder().SetBasePath(currentEnvironment.ContentRootPath).AddEnvironmentVariables();
            builder.Build();
        }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddUksf(_configuration, _currentEnvironment);

            services.AddCors(
                options => options.AddPolicy(
                    "CorsPolicy",
                    builder =>
                    {
                        builder.AllowAnyMethod()
                               .AllowAnyHeader()
                               .WithOrigins("http://localhost:4200", "http://localhost:4300", "https://uk-sf.co.uk", "https://api.uk-sf.co.uk", "https://uk-sf.co.uk")
                               .AllowCredentials();
                    }
                )
            );
            services.AddControllers(options => { options.EnableEndpointRouting = true; });
            services.AddRouting()
                    .AddSwaggerGen(options => { options.SwaggerDoc("v1", new() { Title = "UKSF API", Version = "v1" }); })
                    .AddMvc()
                    .AddNewtonsoftJson(options => { options.SerializerSettings.ContractResolver = new CamelCasePropertyNamesContractResolver(); });
        }

        public void Configure(IApplicationBuilder app, IHostApplicationLifetime hostApplicationLifetime, IServiceProvider serviceProvider)
        {
            hostApplicationLifetime.ApplicationStopping.Register(() => OnShutdown(serviceProvider));

            app.UseStaticFiles()
               .UseCookiePolicy(new() { MinimumSameSitePolicy = SameSiteMode.Lax })
               .UseSwagger()
               .UseSwaggerUI(
                   options =>
                   {
                       options.SwaggerEndpoint("/swagger/v1/swagger.json", "UKSF API v1");
                       options.DocExpansion(DocExpansion.None);
                   }
               )
               .UseRouting()
               .UseCors("CorsPolicy")
               .UseMiddleware<CorsMiddleware>()
               .UseMiddleware<ExceptionMiddleware>()
               .UseAuthentication()
               .UseAuthorization()
               .UseHsts()
               .UseForwardedHeaders(new() { ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto })
               .UseEndpoints(
                   endpoints =>
                   {
                       endpoints.MapControllers().RequireCors("CorsPolicy");
                       endpoints.AddUksfAdminSignalr();
                       endpoints.AddUksfArmaServerSignalr();
                       endpoints.AddUksfCommandSignalr();
                       endpoints.AddUksfIntegrationTeamspeakSignalr();
                       endpoints.AddUksfModpackSignalr();
                       endpoints.AddUksfPersonnelSignalr();
                   }
               );

            serviceProvider.StartUksfServices();
        }

        private static void OnShutdown(IServiceProvider serviceProvider)
        {
            serviceProvider.StopUksfServices();
        }
    }
}
