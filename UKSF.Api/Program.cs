using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.WindowsServices;
using Microsoft.Extensions.Hosting;

namespace UKSF.Api
{
    public static class Program
    {
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

            string environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
            bool isDevelopment = environment == Environments.Development;

            if (isDevelopment)
            {
                BuildDebugWebHost(args).Run();
            }
            else
            {
                InitLogging();
                BuildProductionWebHost(args).RunAsService();
            }
        }

        private static IWebHost BuildDebugWebHost(string[] args)
        {
            return WebHost.CreateDefaultBuilder(args).UseStartup<Startup>().UseKestrel().UseContentRoot(Directory.GetCurrentDirectory()).UseUrls("http://*:5000").Build();
        }

        private static IWebHost BuildProductionWebHost(string[] args)
        {
            return WebHost.CreateDefaultBuilder(args)
                          .UseStartup<Startup>()
                          .UseKestrel(
                              options =>
                              {
                                  options.Listen(IPAddress.Loopback, 5000);
                                  options.Listen(
                                      IPAddress.Loopback,
                                      5001,
                                      listenOptions => { listenOptions.UseHttps("C:\\ProgramData\\win-acme\\acme-v02.api.letsencrypt.org\\Certificates\\uk-sf.co.uk.pfx"); }
                                  );
                              }
                          )
                          .UseContentRoot(Path.GetDirectoryName(Process.GetCurrentProcess().MainModule?.FileName))
                          .Build();
        }

        private static void InitLogging()
        {
            string appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "UKSF.Api");
            Directory.CreateDirectory(appData);
            string[] logFiles = new DirectoryInfo(appData).EnumerateFiles("*.log").OrderByDescending(file => file.LastWriteTime).Select(file => file.Name).ToArray();
            if (logFiles.Length > 9)
            {
                File.Delete(Path.Combine(appData, logFiles.Last()));
            }

            string logFile = Path.Combine(appData, $"LOG__{DateTime.Now:yyyy-MM-dd__HH-mm}.log");
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
