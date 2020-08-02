using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MongoDB.Driver;
using UKSF.Api.Interfaces.Data.Cached;
using UKSF.Api.Interfaces.Personnel;
using UKSF.Api.Models.Command;
using UKSF.Api.Models.Personnel;

namespace UKSF.Api.Services.Personnel {
    public class LoaService : DataBackedService<ILoaDataService>, ILoaService {
        public LoaService(ILoaDataService data) : base(data) { }

        public IEnumerable<Loa> Get(List<string> ids) {
            return Data.Get(x => ids.Contains(x.recipient) && x.end > DateTime.Now.AddDays(-30));
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
            await Data.Add(loa);
            return loa.id;
        }

        public async Task SetLoaState(string id, LoaReviewState state) {
            await Data.Update(id, Builders<Loa>.Update.Set(x => x.state, state));
        }

        public bool IsLoaCovered(string id, DateTime eventStart) {
            return Data.Get(loa => loa.recipient == id && loa.start < eventStart && loa.end > eventStart).Any();
        }
    }
}
