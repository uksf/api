// using System;
// using Microsoft.AspNetCore.Builder;
// using Microsoft.AspNetCore.Http;
// using Microsoft.AspNetCore.HttpOverrides;
// using Microsoft.Extensions.Configuration;
// using Microsoft.Extensions.DependencyInjection;
// using Microsoft.Extensions.Hosting;
// using Microsoft.Extensions.Logging;
// using Newtonsoft.Json.Serialization;
// using Swashbuckle.AspNetCore.SwaggerUI;
// using UKSF.Api.Admin;
// using UKSF.Api.AppStart;
// using UKSF.Api.ArmaServer;
// using UKSF.Api.Command;
// using UKSF.Api.Middleware;
// using UKSF.Api.Modpack;
// using UKSF.Api.Personnel;
// using UKSF.Api.Shared;
// using UKSF.Api.Shared.Events;
// using UKSF.Api.Teamspeak;
//
// namespace UKSF.Api;
//
// public class Startup
// {
//     private readonly IConfiguration _configuration;
//     private readonly IHostEnvironment _currentEnvironment;
//
//     public Startup(IHostEnvironment currentEnvironment, IConfiguration configuration)
//     {
//         _configuration = configuration;
//         _currentEnvironment = currentEnvironment;
//         var builder = new ConfigurationBuilder().SetBasePath(currentEnvironment.ContentRootPath).AddEnvironmentVariables();
//         builder.Build();
//     }
//
//     public void ConfigureServices(IServiceCollection services)
//     {
//         services.AddUksf(_configuration, _currentEnvironment);
//
//         services.AddCors(
//             options => options.AddPolicy(
//                 "CorsPolicy",
//                 builder => { builder.AllowAnyMethod().AllowAnyHeader().WithOrigins(GetCorsPaths()).AllowCredentials(); }
//             )
//         );
//         services.AddControllers(options => { options.EnableEndpointRouting = true; });
//         services.AddRouting()
//                 .AddLogging(builder => { builder.ClearProviders().AddConsole(); })
//                 .AddSwaggerGen(options => { options.SwaggerDoc("v1", new() { Title = "UKSF API", Version = "v1" }); })
//                 .AddMvc()
//                 .AddNewtonsoftJson(options => { options.SerializerSettings.ContractResolver = new CamelCasePropertyNamesContractResolver(); });
//     }
//
//     public void Configure(IApplicationBuilder app, IHostApplicationLifetime hostApplicationLifetime, IServiceProvider serviceProvider)
//     {
//         var logger = serviceProvider.GetService<IUksfLogger>();
//
//         hostApplicationLifetime.ApplicationStopping.Register(
//             () =>
//             {
//                 using var scope = app.ApplicationServices.CreateScope();
//                 OnShutdown(scope.ServiceProvider);
//             }
//         );
//
//         app.UseStaticFiles()
//            .UseCookiePolicy(new() { MinimumSameSitePolicy = SameSiteMode.Lax })
//            .UseSwagger()
//            .UseSwaggerUI(
//                options =>
//                {
//                    options.SwaggerEndpoint("/swagger/v1/swagger.json", "UKSF API v1");
//                    options.DocExpansion(DocExpansion.None);
//                }
//            )
//            .UseRouting()
//            .UseCors("CorsPolicy")
//            .UseMiddleware<CorsMiddleware>()
//            .UseMiddleware<ExceptionMiddleware>()
//            .UseAuthentication()
//            .UseAuthorization()
//            .UseHsts()
//            .UseForwardedHeaders(new() { ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto })
//            .UseEndpoints(
//                endpoints =>
//                {
//                    endpoints.MapControllers().RequireCors("CorsPolicy");
//                    endpoints.AddUksfSignalr();
//                    endpoints.AddUksfAdminSignalr();
//                    endpoints.AddUksfArmaServerSignalr();
//                    endpoints.AddUksfCommandSignalr();
//                    endpoints.AddUksfIntegrationTeamspeakSignalr();
//                    endpoints.AddUksfModpackSignalr();
//                    endpoints.AddUksfPersonnelSignalr();
//                }
//            );
//
//         serviceProvider.StartUksfServices();
//         logger?.LogInfo("Services started");
//     }
//
//     private static void OnShutdown(IServiceProvider serviceProvider)
//     {
//         var logger = serviceProvider.GetService<IUksfLogger>();
//
//         logger?.LogInfo("Shutting down, stopping services");
//         serviceProvider.StopUksfServices();
//         logger?.LogInfo("Services stopped");
//     }
//
//     private string[] GetCorsPaths()
//     {
//         var environment = _configuration.GetSection("appSettings")["environment"];
//         return environment switch
//         {
//             "Development" => new[] { "http://localhost:4200", "http://localhost:4300", "https://dev.uk-sf.co.uk", "https://api-dev.uk-sf.co.uk" },
//             "Production"  => new[] { "https://uk-sf.co.uk", "https://api.uk-sf.co.uk" },
//             _             => throw new ArgumentException($"Invalid environment {environment}", nameof(environment))
//         };
//     }
// }


