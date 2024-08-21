using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Hosting.WindowsServices;
using UKSF.Api.AppStart;
using UKSF.Api.ArmaServer;
using UKSF.Api.Core;
using UKSF.Api.Core.Configuration;
using UKSF.Api.Core.Converters;
using UKSF.Api.Extensions;
using UKSF.Api.Integrations.Teamspeak;
using UKSF.Api.Middleware;
using UKSF.Api.Modpack;

var developmentOrigins = new[] { "http://localhost:4200", "http://localhost:4300", "https://dev.uk-sf.co.uk", "https://api-dev.uk-sf.co.uk" };
var productionOrigins = new[] { "https://uk-sf.co.uk", "https://api.uk-sf.co.uk" };

WebApplicationBuilder builder;
if (WindowsServiceHelpers.IsWindowsService())
{
    builder = WebApplication.CreateBuilder(new WebApplicationOptions { ContentRootPath = AppContext.BaseDirectory, Args = args });
    builder.Host.UseWindowsService();

    var appSettings = new AppSettings();
    builder.Configuration.GetSection(nameof(AppSettings)).Bind(appSettings);

    InitServiceLogging(appSettings);
}
else
{
    builder = WebApplication.CreateBuilder(args);
}

builder.Services.AddUksf(builder.Configuration, builder.Environment);
builder.Services.AddCors(
           options =>
           {
               options.AddPolicy(
                   "CorsPolicy",
                   policy =>
                   {
                       var appSettings = new AppSettings();
                       builder.Configuration.GetSection(nameof(AppSettings)).Bind(appSettings);
                       var origins = appSettings.Environment switch
                       {
                           "Development" => developmentOrigins,
                           "Production"  => productionOrigins,
                           _             => throw new InvalidOperationException($"Invalid environment {appSettings.Environment}")
                       };
                       policy.AllowAnyMethod().AllowAnyHeader().WithOrigins(origins).AllowCredentials();
                   }
               );
           }
       )
       .AddRouting()
       .AddLogging(options => { options.ClearProviders().AddConsole(); })
       .AddControllers()
       .AddJsonOptions(
           options =>
           {
               options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
               options.JsonSerializerOptions.DictionaryKeyPolicy = JsonNamingPolicy.CamelCase;
               options.JsonSerializerOptions.Converters.Add(new InferredTypeConverter());
           }
       );

var app = builder.Build();

var logger = app.Services.GetRequiredService<IUksfLogger>();
app.Lifetime.ApplicationStopping.Register(
    () =>
    {
        using var scope = app.Services.CreateScope();

        logger?.LogInfo("Shutting down, stopping services");
        scope.ServiceProvider.StopUksfServices();
        logger?.LogInfo("Services stopped");
    }
);

app.UseCookiePolicy(new CookiePolicyOptions { MinimumSameSitePolicy = SameSiteMode.Lax })
   .UseRouting()
   .UseCors("CorsPolicy")
   .UseMiddleware<CorsMiddleware>()
   .UseMiddleware<ExceptionMiddleware>()
   .UseAuthentication()
   .UseAuthorization()
   .UseHsts()
   .UseForwardedHeaders(new ForwardedHeadersOptions { ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto })
   .UseEndpoints(
       endpoints =>
       {
           endpoints.MapControllers().RequireCors("CorsPolicy");
           endpoints.AddUksfSharedSignalr();
           endpoints.AddUksfSignalr();
           endpoints.AddUksfArmaServerSignalr();
           endpoints.AddUksfIntegrationTeamspeakSignalr();
           endpoints.AddUksfModpackSignalr();
       }
   );

app.Services.StartUksfServices();
logger.LogInfo("Services started");

app.Run();
return;

void InitServiceLogging(AppSettings appSettings)
{
    var appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), appSettings.LogsPath);
    Directory.CreateDirectory(appData);
    var logFiles = new DirectoryInfo(appData).EnumerateFiles("*.log").OrderByDescending(file => file.LastWriteTime).Select(file => file.Name).ToArray();
    if (logFiles.Length > 9)
    {
        File.Delete(Path.Combine(appData, logFiles.Last()));
    }

    var logFile = Path.Combine(appData, $"LOG__{DateTime.UtcNow:yyyy-MM-dd__HH-mm}.log");
    try
    {
        File.Create(logFile).Close();
    }
    catch (Exception e)
    {
        Console.Out.WriteLine($"Log file not created: {logFile}. {e.Message}");
    }

    FileStream fileStream = new(logFile, FileMode.Create);
    StreamWriter streamWriter = new(fileStream) { AutoFlush = true };
    Console.SetOut(streamWriter);
    Console.SetError(streamWriter);
}
