using UKSF.Api.ArmaServer.Models;
using UKSF.Api.Core.Extensions;
using UKSF.Api.Core.Services;

namespace UKSF.Api.ArmaServer.Services;

public interface IRptLogService
{
    List<RptLogSource> GetLogSources(DomainGameServer server);
    string GetLatestRptFilePath(DomainGameServer server, string source);
    List<string> ReadFullFile(string filePath);
    List<RptLogSearchResult> SearchFile(string filePath, string query);
    IDisposable WatchFile(string filePath, Action<List<string>> onNewContent);
}

public class RptLogService(IVariablesService variablesService) : IRptLogService
{
    public List<RptLogSource> GetLogSources(DomainGameServer server)
    {
        var sources = new List<RptLogSource> { new("Server", true) };

        if (server.NumberHeadlessClients > 0)
        {
            var hcNames = variablesService.GetVariable("SERVER_HEADLESS_NAMES").AsArray();
            for (var i = 0; i < server.NumberHeadlessClients; i++)
            {
                sources.Add(new RptLogSource(hcNames[i], false));
            }
        }

        return sources;
    }

    public string GetLatestRptFilePath(DomainGameServer server, string source)
    {
        var profilesPath = variablesService.GetVariable("SERVER_PATH_PROFILES").AsString();
        var profileDir = source == "Server" ? Path.Combine(profilesPath, server.Name) : Path.Combine(profilesPath, $"{server.Name}{source}");

        if (!Directory.Exists(profileDir))
        {
            return null;
        }

        return Directory.GetFiles(profileDir, "*.rpt").OrderByDescending(Path.GetFileName).FirstOrDefault();
    }

    public List<string> ReadFullFile(string filePath)
    {
        return File.ReadAllLines(filePath).ToList();
    }

    public List<RptLogSearchResult> SearchFile(string filePath, string query)
    {
        var lines = File.ReadAllLines(filePath);
        var results = new List<RptLogSearchResult>();

        for (var i = 0; i < lines.Length; i++)
        {
            if (lines[i].Contains(query, StringComparison.OrdinalIgnoreCase))
            {
                results.Add(new RptLogSearchResult(i, lines[i]));
            }
        }

        return results;
    }

    public IDisposable WatchFile(string filePath, Action<List<string>> onNewContent)
    {
        throw new NotImplementedException();
    }
}
