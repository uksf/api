using System;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerUI;
using UKSF.Api.Admin;
using UKSF.Api.AppStart;
using UKSF.Api.ArmaServer;
using UKSF.Api.Command;
using UKSF.Api.Modpack;
using UKSF.Api.Personnel;
using UKSF.Api.Teamspeak;

namespace UKSF.Api {
    public class Startup {
        private readonly IConfiguration _configuration;
        private readonly IHostEnvironment _currentEnvironment;

        public Startup(IHostEnvironment currentEnvironment, IConfiguration configuration) {
            _configuration = configuration;
            _currentEnvironment = currentEnvironment;
            IConfigurationBuilder builder = new ConfigurationBuilder().SetBasePath(currentEnvironment.ContentRootPath).AddEnvironmentVariables();
            builder.Build();
        }

        public void ConfigureServices(IServiceCollection services) {
            services.AddUksf(_configuration, _currentEnvironment);

            services.AddCors(
                options => options.AddPolicy(
                    "CorsPolicy",
                    builder => {
                        builder.AllowAnyMethod()
                               .AllowAnyHeader()
                               .WithOrigins("http://localhost:4200", "http://localhost:4300", "https://uk-sf.co.uk", "https://api.uk-sf.co.uk", "https://uk-sf.co.uk")
                               .AllowCredentials();
                    }
                )
            );
            services.AddSignalR().AddNewtonsoftJsonProtocol();
            services.AddAutoMapper(typeof(AutoMapperConfigurationProfile));
            services.AddControllers();
            services.AddSwaggerGen(options => { options.SwaggerDoc("v1", new OpenApiInfo { Title = "UKSF API", Version = "v1" }); });
            services.AddMvc(options => { options.Filters.Add<ExceptionHandler>(); }).AddNewtonsoftJson();
        }

        public void Configure(IApplicationBuilder app, IHostApplicationLifetime hostApplicationLifetime, IServiceProvider serviceProvider) {
            hostApplicationLifetime.ApplicationStopping.Register(() => OnShutdown(serviceProvider));

            app.UseStaticFiles();
            app.UseCookiePolicy(new CookiePolicyOptions { MinimumSameSitePolicy = SameSiteMode.Lax });
            app.UseSwagger();
            app.UseSwaggerUI(
                options => {
                    options.SwaggerEndpoint("/swagger/v1/swagger.json", "UKSF API v1");
                    options.DocExpansion(DocExpansion.None);
                }
            );
            app.UseRouting();
            app.UseCors("CorsPolicy");
            app.UseCorsMiddleware();
            app.UseAuthentication();
            app.UseAuthorization();
            app.UseHsts();
            app.UseForwardedHeaders(new ForwardedHeadersOptions { ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto });
            app.UseEndpoints(
                endpoints => {
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

        private static void OnShutdown(IServiceProvider serviceProvider) {
            serviceProvider.StopUksfSerices();
        }
    }

    public class CorsMiddleware {
        private readonly RequestDelegate _next;

        public CorsMiddleware(RequestDelegate next) => _next = next;

        public Task Invoke(HttpContext httpContext) {
            if (httpContext.Request.Path.Value != null && httpContext.Request.Path.Value.Contains("hub")) {
                httpContext.Response.Headers["Access-Control-Allow-Origin"] = httpContext.Request.Headers["Origin"];
                httpContext.Response.Headers["Access-Control-Allow-Credentials"] = "true";
            }

            return _next(httpContext);
        }
    }

    public static class CorsMiddlewareExtensions {
        public static void UseCorsMiddleware(this IApplicationBuilder builder) => builder.UseMiddleware<CorsMiddleware>();
    }
}
