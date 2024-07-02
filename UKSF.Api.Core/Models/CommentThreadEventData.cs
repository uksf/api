namespace UKSF.Api.Core.Models;

public class CommentThreadEventData(string commentThreadId, Comment comment) : EventData
{
    public Comment Comment { get; } = comment;
    public string CommentThreadId { get; } = commentThreadId;
}
