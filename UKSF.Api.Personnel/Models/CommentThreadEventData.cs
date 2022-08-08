namespace UKSF.Api.Personnel.Models;

public class CommentThreadEventData
{
    public Comment Comment { get; set; }
    public string CommentThreadId { get; set; }

    public CommentThreadEventData(string commentThreadId, Comment comment)
    {
        CommentThreadId = commentThreadId;
        Comment = comment;
    }
}
