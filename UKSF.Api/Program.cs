using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Hosting.WindowsServices;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerUI;
using UKSF.Api.AppStart;
using UKSF.Api.ArmaServer;
using UKSF.Api.Extensions;
using UKSF.Api.Middleware;
using UKSF.Api.Modpack;
using UKSF.Api.Shared;
using UKSF.Api.Shared.Configuration;
using UKSF.Api.Shared.Converters;
using UKSF.Api.Teamspeak;

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
       .AddSwaggerGen(
           options =>
           {
               options.SwaggerDoc("v1", new() { Title = "UKSF API", Version = "v1" });
               options.AddSecurityDefinition(
                   "Bearer",
                   new()
                   {
                       Name = "Authorization",
                       Type = SecuritySchemeType.Http,
                       Scheme = "Bearer",
                       In = ParameterLocation.Header,
                       Description = "JSON Web Token based security"
                   }
               );
               options.AddSecurityRequirement(
                   new()
                   {
                       {
                           new()
                           {
                               Reference = new() { Type = ReferenceType.SecurityScheme, Id = "Bearer" },
                               Scheme = "oauth2",
                               Name = "Bearer",
                               In = ParameterLocation.Header
                           },
                           new List<string>()
                       }
                   }
               );
           }
       )
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

app.UseCookiePolicy(new() { MinimumSameSitePolicy = SameSiteMode.Lax })
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

/*
public static class Program
{
    private static IConfigurationRoot _config;

    public static void Main(string[] args)
    {
        if (!OperatingSystem.IsWindows())
        {
            Console.Out.WriteLine("Not running on windows, shutting down.");
            return;
        }

        // AppDomain.CurrentDomain.GetAssemblies()
        //          .ToList()
        //          .SelectMany(x => x.GetReferencedAssemblies())
        //          .Distinct()
        //          .Where(y => AppDomain.CurrentDomain.GetAssemblies().ToList().Any(a => a.FullName == y.FullName) == false)
        //          .ToList()
        //          .ForEach(x => AppDomain.CurrentDomain.GetAssemblies().ToList().Add(AppDomain.CurrentDomain.Load(x)));

        _config = new ConfigurationBuilder().AddJsonFile("appsettings.json").Build();

        var runAsService = bool.Parse(_config.GetSection("appSettings")["runAsService"]);
        if (runAsService)
        {
            InitServiceLogging();
            BuildProductionWebHost(args).RunAsService();
        }
        else
        {
            BuildDebugWebHost(args).Run();
        }
    }

    private static IWebHost BuildDebugWebHost(string[] args)
    {
        return WebHost.CreateDefaultBuilder(args)
                      .UseStartup<Startup>()
                      .UseKestrel()
                      // .UseContentRoot(Directory.GetCurrentDirectory())
                      .Build();
    }

    private static IWebHost BuildProductionWebHost(string[] args)
    {
        return WebHost.CreateDefaultBuilder(args)
                      .UseStartup<Startup>()
                      .UseKestrel()
                      // .UseContentRoot(Path.GetDirectoryName(Process.GetCurrentProcess().MainModule?.FileName))
                      .Build();
    }

    private static void InitServiceLogging()
    {
        var logsPath = _config.GetSection("appSettings")["logsPath"];
        var appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), logsPath);
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
}
*/
