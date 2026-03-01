namespace UKSF.Api.ArmaServer.Models;

public record RptLogSource(string Name, bool IsServer);

public record RptLogContent(List<string> Lines, int StartLineIndex, bool IsComplete);

public record RptLogSearchResult(int LineIndex, string Text);

public record RptLogSearchResponse(List<RptLogSearchResult> Results, int TotalMatches);
