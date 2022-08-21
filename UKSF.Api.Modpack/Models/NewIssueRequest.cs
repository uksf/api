namespace UKSF.Api.Modpack.Models
{
    public class NewIssueRequest
    {
        public NewIssueType IssueType { get; set; }
        public string Title { get; set; }
        public string Body { get; set; }
    }

    public enum NewIssueType
    {
        WEBSITE,
        MODPACK
    }
}
