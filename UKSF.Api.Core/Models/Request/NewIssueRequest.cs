namespace UKSF.Api.Core.Models.Request;

public record NewIssueRequest(string Title, List<string> Labels, string Body);
