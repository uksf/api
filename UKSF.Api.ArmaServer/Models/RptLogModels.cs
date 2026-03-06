namespace UKSF.Api.ArmaServer.Models;

public record RptLogSource(string Name, bool IsServer);

public record RptLogSearchResult(int LineIndex, string Text);

public record RptLogSearchResponse(List<RptLogSearchResult> Results, int TotalMatches);

public record LogSearchRequest(string Source, string Query);
