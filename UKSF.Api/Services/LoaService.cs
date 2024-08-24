using MongoDB.Driver;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Exceptions;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Models.Request;

namespace UKSF.Api.Services;

public interface ILoaService
{
    IEnumerable<DomainLoa> Get(List<string> ids);
    Task<string> Add(CreateLoaRequest createLoaRequest, string recipient, string reason);
    Task SetLoaState(string id, LoaReviewState state);
    bool IsLoaCovered(string id, DateTime eventStart);
}

public class LoaService(ILoaContext loaContext) : ILoaService
{
    public IEnumerable<DomainLoa> Get(List<string> ids)
    {
        return loaContext.Get(x => ids.Contains(x.Recipient));
    }

    public async Task<string> Add(CreateLoaRequest createLoaRequest, string recipient, string reason)
    {
        if (loaContext.Get(x => x.Recipient == recipient && x.State != LoaReviewState.Rejected)
                      .Any(x => createLoaRequest.Start >= x.Start && createLoaRequest.End <= x.End))
        {
            throw new BadRequestException("An LOA covering the same date range already exists");
        }

        DomainLoa loa = new()
        {
            Submitted = DateTime.UtcNow,
            Recipient = recipient,
            Start = createLoaRequest.Start,
            End = createLoaRequest.End,
            Reason = reason,
            Emergency = createLoaRequest.Emergency,
            Late = createLoaRequest.Late
        };
        await loaContext.Add(loa);
        return loa.Id;
    }

    public async Task SetLoaState(string id, LoaReviewState state)
    {
        await loaContext.Update(id, Builders<DomainLoa>.Update.Set(x => x.State, state));
    }

    public bool IsLoaCovered(string id, DateTime eventStart)
    {
        return loaContext.Get(loa => loa.Recipient == id && loa.Start < eventStart && loa.End > eventStart).Any();
    }
}

