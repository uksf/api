using System;
using System.Collections.Generic;

namespace UKSF.Api.Personnel.Models
{
    public class CommentThreadsDataset
    {
        public IEnumerable<CommentThreadDataset> Comments;
    }

    public class CommentThreadDataset
    {
        public string Author;
        public string Content;
        public string DisplayName;
        public string Id;
        public DateTime Timestamp;
    }
}
