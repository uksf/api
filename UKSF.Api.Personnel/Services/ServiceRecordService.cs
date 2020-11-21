using System;
using MongoDB.Driver;
using UKSF.Api.Personnel.Context;
using UKSF.Api.Personnel.Models;

namespace UKSF.Api.Personnel.Services {
    public interface IServiceRecordService {
        void AddServiceRecord(string id, string occurence, string notes);
    }

    public class ServiceRecordService : IServiceRecordService {
        private readonly IAccountContext _accountContext;

        public ServiceRecordService(IAccountContext accountContext) => _accountContext = accountContext;

        public void AddServiceRecord(string id, string occurence, string notes) {
            _accountContext.Update(id, Builders<Account>.Update.Push("serviceRecord", new ServiceRecordEntry { Timestamp = DateTime.Now, Occurence = occurence, Notes = notes }));
        }
    }
}
