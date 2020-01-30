using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.WindowsServices;
using Microsoft.Extensions.Hosting;

namespace UKSF.Api {
    public static class Program {
        public static void Main(string[] args) {
            AppDomain.CurrentDomain.GetAssemblies()
                     .ToList()
                     .SelectMany(x => x.GetReferencedAssemblies())
                     .Distinct()
                     .Where(y => AppDomain.CurrentDomain.GetAssemblies().ToList().Any(a => a.FullName == y.FullName) == false)
                     .ToList()
                     .ForEach(x => AppDomain.CurrentDomain.GetAssemblies().ToList().Add(AppDomain.CurrentDomain.Load(x)));

            string environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
            bool isDevelopment = environment == Environments.Development;

            if (isDevelopment) {
                BuildDebugWebHost(args).Run();
            } else {
                InitLogging();
                BuildProductionWebHost(args).RunAsService();
            }
        }

        private static IWebHost BuildDebugWebHost(string[] args) =>
            WebHost.CreateDefaultBuilder(args)
                   .UseStartup<Startup>()
                   .UseKestrel()
                   .UseContentRoot(Directory.GetCurrentDirectory())
                   .UseUrls("http://*:5000")
                   .UseIISIntegration()
                   .Build();

        private static IWebHost BuildProductionWebHost(string[] args) =>
            WebHost.CreateDefaultBuilder(args)
                   .UseStartup<Startup>()
                   .UseKestrel(
                       options => {
                           options.Listen(IPAddress.Loopback, 5000);
                           options.Listen(IPAddress.Loopback, 5001, listenOptions => { listenOptions.UseHttps("C:\\ProgramData\\win-acme\\httpsacme-v01.api.letsencrypt.org\\uk-sf.co.uk-all.pfx"); });
                       }
                   )
                   .UseContentRoot(Path.GetDirectoryName(Process.GetCurrentProcess().MainModule?.FileName))
                   .UseIISIntegration()
                   .Build();

        private static void InitLogging() {
            string appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "UKSF.ApiApi");
            Directory.CreateDirectory(appData);
            string[] logFiles = new DirectoryInfo(appData).EnumerateFiles("*.log").OrderByDescending(file => file.LastWriteTime).Select(file => file.Name).ToArray();
            if (logFiles.Length > 9) {
                File.Delete(Path.Combine(appData, logFiles.Last()));
            }

            string logFile = Path.Combine(appData, $"LOG__{DateTime.Now:yyyy-MM-dd__HH-mm}.log");
            try {
                File.Create(logFile).Close();
            } catch (Exception e) {
                Console.WriteLine($"Log file not created: {logFile}. {e.Message}");
            }

            FileStream fileStream = new FileStream(logFile, FileMode.Create);
            StreamWriter streamWriter = new StreamWriter(fileStream) {AutoFlush = true};
            Console.SetOut(streamWriter);
            Console.SetError(streamWriter);
        }
    }
}
