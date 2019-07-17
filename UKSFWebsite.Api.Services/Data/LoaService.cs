using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MongoDB.Driver;
using UKSFWebsite.Api.Models;
using UKSFWebsite.Api.Models.CommandRequests;
using UKSFWebsite.Api.Services.Abstraction;

namespace UKSFWebsite.Api.Services.Data {
    public class LoaService : CachedDataService<Loa>, ILoaService {
        public LoaService(IMongoDatabase database) : base(database, "loas") { }

        public IEnumerable<Loa> Get(List<string> ids) {
            return Get(x => ids.Contains(x.recipient) && x.end > DateTime.Now.AddDays(-30));
        }

        public async Task<string> Add(CommandRequestLoa requestBase) {
            Loa loa = new Loa {
                submitted = DateTime.Now,
                recipient = requestBase.recipient,
                start = requestBase.start,
                end = requestBase.end,
                reason = requestBase.reason,
                emergency = !string.IsNullOrEmpty(requestBase.emergency) && bool.Parse(requestBase.emergency),
                late = !string.IsNullOrEmpty(requestBase.late) && bool.Parse(requestBase.late)
            };
            await base.Add(loa);
            Refresh();
            return loa.id;
        }

        public async Task SetLoaState(string id, LoaReviewState state) {
            await Update(id, Builders<Loa>.Update.Set(x => x.state, state));
            Refresh();
        }

        public bool IsLoaCovered(string id, DateTime eventStart) {
            return Get(loa => loa.recipient == id && loa.start < eventStart && loa.end > eventStart).Count > 0;
        }
    }
}
