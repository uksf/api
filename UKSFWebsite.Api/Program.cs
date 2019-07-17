// ReSharper disable RedundantUsingDirective

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.WindowsServices;

namespace UKSFWebsite.Api {
    public class Program {
        public static void Main(string[] args) {
            AppDomain.CurrentDomain.GetAssemblies()
                     .ToList()
                     .SelectMany(x => x.GetReferencedAssemblies())
                     .Distinct()
                     .Where(y => AppDomain.CurrentDomain.GetAssemblies().ToList().Any(a => a.FullName == y.FullName) == false)
                     .ToList()
                     .ForEach(x => AppDomain.CurrentDomain.GetAssemblies().ToList().Add(AppDomain.CurrentDomain.Load(x)));

#if DEBUG
            BuildWebHost(args).Run();
#else
            InitLogging();
            BuildWebHost(args).RunAsService();
#endif
        }

#if DEBUG
        private static IWebHost BuildWebHost(string[] args) =>
            WebHost.CreateDefaultBuilder(args).UseStartup<Startup>().UseKestrel().UseContentRoot(Directory.GetCurrentDirectory()).UseUrls("http://*:5000").UseIISIntegration().Build();
#else
        private static IWebHost BuildWebHost(string[] args) =>
            WebHost.CreateDefaultBuilder(args).UseStartup<Startup>().UseKestrel(
                       options => {
                           options.Listen(IPAddress.Loopback, 5000);
                           options.Listen(IPAddress.Loopback, 5001, listenOptions => { listenOptions.UseHttps("C:\\ProgramData\\win-acme\\httpsacme-v01.api.letsencrypt.org\\uk-sf.co.uk-all.pfx"); });
                       }).UseContentRoot(Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName))
                   .UseIISIntegration().Build();

        private static void InitLogging() {
            string appdata = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "UKSFWebsiteApi");
            Directory.CreateDirectory(appdata);
            string[] logFiles = new DirectoryInfo(appdata).EnumerateFiles("*.log").OrderByDescending(file => file.LastWriteTime).Select(file => file.Name).ToArray();
            if (logFiles.Length > 9) {
                File.Delete(Path.Combine(appdata, logFiles.Last()));
            }

            string logFile = Path.Combine(appdata, $"LOG__{DateTime.Now:yyyy-MM-dd__HH-mm}.log");
            try {
                File.Create(logFile).Close();
            } catch (Exception e) {
                Console.WriteLine($"Log file not created: {logFile}. {e.Message}");
            }

            FileStream filestream = new FileStream(logFile, FileMode.Create);
            StreamWriter streamwriter = new StreamWriter(filestream) {AutoFlush = true};
            Console.SetOut(streamwriter);
            Console.SetError(streamwriter);
        }
#endif
    }
}
