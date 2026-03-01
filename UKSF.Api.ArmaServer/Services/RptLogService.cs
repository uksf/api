using UKSF.Api.ArmaServer.Models;
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
        throw new NotImplementedException();
    }

    public string GetLatestRptFilePath(DomainGameServer server, string source)
    {
        throw new NotImplementedException();
    }

    public List<string> ReadFullFile(string filePath)
    {
        throw new NotImplementedException();
    }

    public List<RptLogSearchResult> SearchFile(string filePath, string query)
    {
        throw new NotImplementedException();
    }

    public IDisposable WatchFile(string filePath, Action<List<string>> onNewContent)
    {
        throw new NotImplementedException();
    }
}
