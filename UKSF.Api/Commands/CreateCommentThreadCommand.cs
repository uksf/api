using UKSF.Api.Core.Context;
using UKSF.Api.Core.Models;

namespace UKSF.Api.Commands;

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
