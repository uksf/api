namespace UKSF.Api.Core.Models;

public record IssueTemplate(string Name, string Description, string Title, List<string> Labels, string Body);
