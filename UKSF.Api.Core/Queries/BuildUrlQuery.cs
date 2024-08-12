using Microsoft.AspNetCore.WebUtilities;
using UKSF.Api.Core.Configuration;

namespace UKSF.Api.Core.Queries;

public interface IBuildUrlQuery
{
    string Web(string path, IDictionary<string, string> query = null);
    string Api(string path, IDictionary<string, string> query = null);
}

public class BuildUrlQuery : IBuildUrlQuery
{
    private readonly AppSettings _appSettings;

    public BuildUrlQuery(AppSettings appSettings)
    {
        _appSettings = appSettings;
    }

    public string Web(string path, IDictionary<string, string> query = null)
    {
        var uri = BuildUri(_appSettings.WebUrl, path);
        return query == null ? uri : BuildUriWithQueryParams(uri, query);
    }

    public string Api(string path, IDictionary<string, string> query = null)
    {
        var uri = BuildUri(_appSettings.ApiUrl, path);
        return query == null ? uri : BuildUriWithQueryParams(uri, query);
    }

    private static string BuildUri(string baseUri, string path)
    {
        return new Uri(new Uri(baseUri), path).ToString();
    }

    private static string BuildUriWithQueryParams(string uri, IDictionary<string, string> query)
    {
        return QueryHelpers.AddQueryString(uri, query);
    }
}
