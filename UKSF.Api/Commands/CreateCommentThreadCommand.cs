using UKSF.Api.Core.Context;
using UKSF.Api.Core.Models.Domain;

namespace UKSF.Api.Commands;

public interface ICreateCommentThreadCommand
{
    Task<DomainCommentThread> ExecuteAsync(string[] authors, ThreadMode mode);
}

public class CreateCommentThreadCommand(ICommentThreadContext commentThreadContext) : ICreateCommentThreadCommand
{
    public async Task<DomainCommentThread> ExecuteAsync(string[] authors, ThreadMode mode)
    {
        var commentThread = new DomainCommentThread { Authors = authors, Mode = mode };
        await commentThreadContext.Add(commentThread);

        return commentThread;
    }
}
