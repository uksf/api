namespace UKSF.Api.Personnel.Models {
    public class CommentThreadEventData {
        public CommentThreadEventData(string commentThreadId, Comment comment) {
            CommentThreadId = commentThreadId;
            Comment = comment;
        }

        public string CommentThreadId { get; }
        public Comment Comment { get; }
    }
}
