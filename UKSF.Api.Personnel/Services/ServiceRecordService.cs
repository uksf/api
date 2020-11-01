using System;
using MongoDB.Driver;
using UKSF.Api.Personnel.Models;

namespace UKSF.Api.Personnel.Services {
    public interface IServiceRecordService {
        void AddServiceRecord(string id, string occurence, string notes);
    }

    public class ServiceRecordService : IServiceRecordService {
        private readonly IAccountService accountService;

        public ServiceRecordService(IAccountService accountService) => this.accountService = accountService;

        public void AddServiceRecord(string id, string occurence, string notes) {
            accountService.Data.Update(id, Builders<Account>.Update.Push("serviceRecord", new ServiceRecordEntry {timestamp = DateTime.Now, occurence = occurence, notes = notes}));
        }
    }
}
