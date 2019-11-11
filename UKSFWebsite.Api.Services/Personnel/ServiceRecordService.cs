using System;
using MongoDB.Driver;
using UKSFWebsite.Api.Interfaces.Personnel;
using UKSFWebsite.Api.Models.Personnel;

namespace UKSFWebsite.Api.Services.Personnel {
    public class ServiceRecordService : IServiceRecordService {
        private readonly IAccountService accountService;

        public ServiceRecordService(IAccountService accountService) => this.accountService = accountService;

        public void AddServiceRecord(string id, string occurence, string notes) {
            accountService.Data().Update(id, Builders<Account>.Update.Push("serviceRecord", new ServiceRecordEntry {timestamp = DateTime.Now, occurence = occurence, notes = notes}));
        }
    }
}
