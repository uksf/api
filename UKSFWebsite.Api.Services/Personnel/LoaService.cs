using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MongoDB.Driver;
using UKSFWebsite.Api.Interfaces.Data.Cached;
using UKSFWebsite.Api.Interfaces.Personnel;
using UKSFWebsite.Api.Models.Command;
using UKSFWebsite.Api.Models.Personnel;

namespace UKSFWebsite.Api.Services.Personnel {
    public class LoaService : ILoaService {
        private readonly ILoaDataService data;

        public LoaService(ILoaDataService data) => this.data = data;

        public ILoaDataService Data() => data;

        public IEnumerable<Loa> Get(List<string> ids) {
            return data.Get(x => ids.Contains(x.recipient) && x.end > DateTime.Now.AddDays(-30));
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
            await data.Add(loa);
            return loa.id;
        }

        public async Task SetLoaState(string id, LoaReviewState state) {
            await data.Update(id, Builders<Loa>.Update.Set(x => x.state, state));
        }

        public bool IsLoaCovered(string id, DateTime eventStart) {
            return data.Get(loa => loa.recipient == id && loa.start < eventStart && loa.end > eventStart).Count > 0;
        }
    }
}
