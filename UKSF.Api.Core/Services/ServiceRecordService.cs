using MongoDB.Driver;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Models;

namespace UKSF.Api.Core.Services;

public interface IServiceRecordService
{
    void AddServiceRecord(string id, string occurence, string notes);
}

public class ServiceRecordService : IServiceRecordService
{
    private readonly IAccountContext _accountContext;

    public ServiceRecordService(IAccountContext accountContext)
    {
        _accountContext = accountContext;
    }

    public void AddServiceRecord(string id, string occurence, string notes)
    {
        _accountContext.Update(
            id,
            Builders<DomainAccount>.Update.Push("serviceRecord", new ServiceRecordEntry { Timestamp = DateTime.UtcNow, Occurence = occurence, Notes = notes })
        );
    }
}
