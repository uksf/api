using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MongoDB.Driver;
using UKSF.Api.Command.Context;
using UKSF.Api.Command.Models;
using UKSF.Api.Personnel.Models;

namespace UKSF.Api.Command.Services {
    public interface ILoaService {
        IEnumerable<Loa> Get(List<string> ids);
        Task<string> Add(CommandRequestLoa requestBase);
        Task SetLoaState(string id, LoaReviewState state);
        bool IsLoaCovered(string id, DateTime eventStart);
    }

    public class LoaService : ILoaService {
        private readonly ILoaContext _loaContext;

        public LoaService(ILoaContext loaContext) => _loaContext = loaContext;

        public IEnumerable<Loa> Get(List<string> ids) {
            return _loaContext.Get(x => ids.Contains(x.Recipient) && x.End > DateTime.Now.AddDays(-30));
        }

        public async Task<string> Add(CommandRequestLoa requestBase) {
            Loa loa = new() {
                Submitted = DateTime.Now,
                Recipient = requestBase.Recipient,
                Start = requestBase.Start,
                End = requestBase.End,
                Reason = requestBase.Reason,
                Emergency = !string.IsNullOrEmpty(requestBase.Emergency) && bool.Parse(requestBase.Emergency),
                Late = !string.IsNullOrEmpty(requestBase.Late) && bool.Parse(requestBase.Late)
            };
            await _loaContext.Add(loa);
            return loa.Id;
        }

        public async Task SetLoaState(string id, LoaReviewState state) {
            await _loaContext.Update(id, Builders<Loa>.Update.Set(x => x.State, state));
        }

        public bool IsLoaCovered(string id, DateTime eventStart) {
            return _loaContext.Get(loa => loa.Recipient == id && loa.Start < eventStart && loa.End > eventStart).Any();
        }
    }
}
