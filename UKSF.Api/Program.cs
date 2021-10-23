using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.WindowsServices;
using Microsoft.Extensions.Configuration;

namespace UKSF.Api
{
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

            AppDomain.CurrentDomain.GetAssemblies()
                     .ToList()
                     .SelectMany(x => x.GetReferencedAssemblies())
                     .Distinct()
                     .Where(y => AppDomain.CurrentDomain.GetAssemblies().ToList().Any(a => a.FullName == y.FullName) == false)
                     .ToList()
                     .ForEach(x => AppDomain.CurrentDomain.GetAssemblies().ToList().Add(AppDomain.CurrentDomain.Load(x)));

            _config = new ConfigurationBuilder().AddJsonFile("appsettings.json").Build();

            var runAsService = bool.Parse(_config.GetSection("appSettings")["runAsService"]);
            if (runAsService)
            {
                InitLogging();
                BuildProductionWebHost(args).RunAsService();
            }
            else
            {
                BuildDebugWebHost(args).Run();
            }
        }

        private static IWebHost BuildDebugWebHost(string[] args)
        {
            var port = _config.GetSection("appSettings")["port"];
            return WebHost.CreateDefaultBuilder(args).UseStartup<Startup>().UseKestrel().UseContentRoot(Directory.GetCurrentDirectory()).UseUrls($"http://*:{port}").Build();
        }

        private static IWebHost BuildProductionWebHost(string[] args)
        {
            var port = int.Parse(_config.GetSection("appSettings")["port"]);
            var portSsl = int.Parse(_config.GetSection("appSettings")["portSsl"]);
            var certificatePath = _config.GetSection("appSettings")["certificatePath"];
            return WebHost.CreateDefaultBuilder(args)
                          .UseStartup<Startup>()
                          .UseKestrel(
                              options =>
                              {
                                  options.Listen(IPAddress.Loopback, port);
                                  options.Listen(IPAddress.Loopback, portSsl, listenOptions => { listenOptions.UseHttps(certificatePath); });
                              }
                          )
                          .UseContentRoot(Path.GetDirectoryName(Process.GetCurrentProcess().MainModule?.FileName))
                          .Build();
        }

        private static void InitLogging()
        {
            var certificatePath = _config.GetSection("appSettings")["logsPath"];
            var appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), certificatePath);
            Directory.CreateDirectory(appData);
            var logFiles = new DirectoryInfo(appData).EnumerateFiles("*.log").OrderByDescending(file => file.LastWriteTime).Select(file => file.Name).ToArray();
            if (logFiles.Length > 9)
            {
                File.Delete(Path.Combine(appData, logFiles.Last()));
            }

            var logFile = Path.Combine(appData, $"LOG__{DateTime.Now:yyyy-MM-dd__HH-mm}.log");
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
}
