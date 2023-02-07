// using Microsoft.AspNetCore.HttpOverrides;
// using Microsoft.OpenApi.Models;
// using Swashbuckle.AspNetCore.SwaggerUI;
// using UKSF.Api.Admin;
// using UKSF.Api.AppStart;
// using UKSF.Api.ArmaServer;
// using UKSF.Api.Base.Configuration;
// using UKSF.Api.Command;
// using UKSF.Api.Middleware;
// using UKSF.Api.Modpack;
// using UKSF.Api.Personnel;
// using UKSF.Api.Core;
// using UKSF.Api.Core.Events;
// using UKSF.Api.Teamspeak;
//
// namespace UKSF.Api;
//
// public class Startup
// {
//     private readonly IConfiguration _configuration;
//     private readonly IHostEnvironment _currentEnvironment;
//
//     private readonly string[] _developmentOrigins =
//     {
//         "http://localhost:4200", "http://localhost:4300", "https://dev.uk-sf.co.uk", "https://api-dev.uk-sf.co.uk"
//     };
//
//     private readonly string[] _productionOrigins = { "https://uk-sf.co.uk", "https://api.uk-sf.co.uk" };
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
//                     options =>
//                     {
//                         options.AddPolicy(
//                             "CorsPolicy",
//                             policy =>
//                             {
//                                 var appSettings = new AppSettings();
//                                 _configuration.GetSection(nameof(AppSettings)).Bind(appSettings);
//                                 var origins = appSettings.Environment switch
//                                 {
//                                     "Development" => _developmentOrigins,
//                                     "Production"  => _productionOrigins,
//                                     _             => throw new InvalidOperationException($"Invalid environment {appSettings.Environment}")
//                                 };
//                                 policy.AllowAnyMethod().AllowAnyHeader().WithOrigins(origins).AllowCredentials();
//                             }
//                         );
//                     }
//                 )
//                 .AddRouting()
//                 .AddLogging(options => { options.ClearProviders().AddConsole(); })
//                 .AddSwaggerGen(
//                     options =>
//                     {
//                         options.SwaggerDoc("v1", new() { Title = "UKSF API", Version = "v1" });
//                         options.AddSecurityDefinition(
//                             "Bearer",
//                             new()
//                             {
//                                 Name = "Authorization",
//                                 Type = SecuritySchemeType.Http,
//                                 Scheme = "Bearer",
//                                 In = ParameterLocation.Header,
//                                 Description = "JSON Web Token based security"
//                             }
//                         );
//                         options.AddSecurityRequirement(
//                             new()
//                             {
//                                 {
//                                     new()
//                                     {
//                                         Reference = new() { Type = ReferenceType.SecurityScheme, Id = "Bearer" },
//                                         Scheme = "oauth2",
//                                         Name = "Bearer",
//                                         In = ParameterLocation.Header
//                                     },
//                                     new List<string>()
//                                 }
//                             }
//                         );
//                     }
//                 )
//                 .AddControllers()
//                 .AddJsonOptions(options => { options.JsonSerializerOptions.DictionaryKeyPolicy = JsonNamingPolicy.CamelCase; });
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
//         app.UseCookiePolicy(new() { MinimumSameSitePolicy = SameSiteMode.Lax })
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
// }


