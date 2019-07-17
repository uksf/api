using System;
using MongoDB.Driver;
using UKSFWebsite.Api.Models;
using UKSFWebsite.Api.Models.Accounts;
using UKSFWebsite.Api.Services.Abstraction;

namespace UKSFWebsite.Api.Services {
    public class ServiceRecordService : IServiceRecordService {
        private readonly IAccountService accountService;

        public ServiceRecordService(IAccountService accountService) => this.accountService = accountService;

        public void AddServiceRecord(string id, string occurence, string notes) {
            accountService.Update(id, Builders<Account>.Update.Push("serviceRecord", new ServiceRecordEntry {timestamp = DateTime.Now, occurence = occurence, notes = notes}));
        }
    }
}
