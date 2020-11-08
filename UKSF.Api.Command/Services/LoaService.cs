using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MongoDB.Driver;
using UKSF.Api.Base.Context;
using UKSF.Api.Command.Context;
using UKSF.Api.Command.Models;
using UKSF.Api.Personnel.Models;

namespace UKSF.Api.Command.Services {
    public interface ILoaService : IDataBackedService<ILoaDataService> {
        IEnumerable<Loa> Get(List<string> ids);
        Task<string> Add(CommandRequestLoa requestBase);
        Task SetLoaState(string id, LoaReviewState state);
        bool IsLoaCovered(string id, DateTime eventStart);
    }

    public class LoaService : DataBackedService<ILoaDataService>, ILoaService {
        public LoaService(ILoaDataService data) : base(data) { }

        public IEnumerable<Loa> Get(List<string> ids) {
            return Data.Get(x => ids.Contains(x.recipient) && x.end > DateTime.Now.AddDays(-30));
        }

        public async Task<string> Add(CommandRequestLoa requestBase) {
            Loa loa = new Loa {
                submitted = DateTime.Now,
                recipient = requestBase.Recipient,
                start = requestBase.Start,
                end = requestBase.End,
                reason = requestBase.Reason,
                emergency = !string.IsNullOrEmpty(requestBase.Emergency) && bool.Parse(requestBase.Emergency),
                late = !string.IsNullOrEmpty(requestBase.Late) && bool.Parse(requestBase.Late)
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
