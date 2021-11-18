using System.Threading.Tasks;
using UKSF.Api.Personnel.Context;
using UKSF.Api.Personnel.Models;

namespace UKSF.Api.Personnel.Commands
{
    public interface ICreateCommentThreadCommand
    {
        Task<CommentThread> ExecuteAsync(string[] authors, ThreadMode mode);
    }

    public class CreateCommentThreadCommand : ICreateCommentThreadCommand
    {
        private readonly ICommentThreadContext _commentThreadContext;

        public CreateCommentThreadCommand(ICommentThreadContext commentThreadContext)
        {
            _commentThreadContext = commentThreadContext;
        }

        public async Task<CommentThread> ExecuteAsync(string[] authors, ThreadMode mode)
        {
            var commentThread = new CommentThread { Authors = authors, Mode = mode };
            await _commentThreadContext.Add(commentThread);

            return commentThread;
        }
    }
}
