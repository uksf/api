namespace UKSF.Api.Personnel.Models
{
    public class CommentThreadEventData
    {
        public Comment Comment;

        public string CommentThreadId;

        public CommentThreadEventData(string commentThreadId, Comment comment)
        {
            CommentThreadId = commentThreadId;
            Comment = comment;
        }
    }
}
