namespace UKSF.Api.Shared.Models;

public class CommentThreadEventData
{
    public CommentThreadEventData(string commentThreadId, Comment comment)
    {
        CommentThreadId = commentThreadId;
        Comment = comment;
    }

    public Comment Comment { get; set; }
    public string CommentThreadId { get; set; }
}
