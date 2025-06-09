using UKSF.Api.Core.Models.Domain;

namespace UKSF.Api.Core.Models;

public class CommentThreadEventData(string commentThreadId, DomainComment comment) : EventData
{
    public DomainComment Comment { get; } = comment;
    public string CommentThreadId { get; } = commentThreadId;
}
