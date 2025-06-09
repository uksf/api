namespace UKSF.Api.Core.Models;

public class CommentThreadsDataset
{
    public IEnumerable<CommentThreadDataset> Comments { get; set; }
}

public class CommentThreadDataset
{
    public string Author { get; set; }
    public string Content { get; set; }
    public string DisplayName { get; set; }
    public string Id { get; set; }
    public DateTime Timestamp { get; set; }
}
