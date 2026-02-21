using UKSF.Api.Core.Context;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Core.Services;

namespace UKSF.Api.Services;

public interface ICommentThreadService
{
    IEnumerable<DomainComment> GetCommentThreadComments(string commentThreadId);
    Task InsertComment(string commentThreadId, DomainComment comment);
    Task RemoveComment(string commentThreadId, DomainComment comment);
    IEnumerable<string> GetCommentThreadParticipants(string commentThreadId);
    object FormatComment(DomainComment comment);
}

public class CommentThreadService(ICommentThreadContext commentThreadContext, IDisplayNameService displayNameService) : ICommentThreadService
{
    public IEnumerable<DomainComment> GetCommentThreadComments(string commentThreadId)
    {
        var thread = commentThreadContext.GetSingle(commentThreadId);
        return thread?.Comments.Reverse() ?? Enumerable.Empty<DomainComment>();
    }

    public async Task InsertComment(string commentThreadId, DomainComment comment)
    {
        await commentThreadContext.AddCommentToThread(commentThreadId, comment);
    }

    public async Task RemoveComment(string commentThreadId, DomainComment comment)
    {
        await commentThreadContext.RemoveCommentFromThread(commentThreadId, comment);
    }

    public IEnumerable<string> GetCommentThreadParticipants(string commentThreadId)
    {
        var participants = GetCommentThreadComments(commentThreadId).Select(x => x.Author).ToHashSet();
        var thread = commentThreadContext.GetSingle(commentThreadId);
        if (thread != null)
        {
            participants.UnionWith(thread.Authors);
        }

        return participants;
    }

    public object FormatComment(DomainComment comment)
    {
        return new
        {
            comment.Id,
            comment.Author,
            comment.Content,
            DisplayName = displayNameService.GetDisplayName(comment.Author),
            comment.Timestamp
        };
    }
}
