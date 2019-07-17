using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UKSFWebsite.Api.Models;
using UKSFWebsite.Api.Models.CommandRequests;

namespace UKSFWebsite.Api.Services.Abstraction {
    public interface ILoaService : IDataService<Loa> {
        IEnumerable<Loa> Get(List<string> ids);
        Task<string> Add(CommandRequestLoa requestBase);
        Task SetLoaState(string id, LoaReviewState state);
        bool IsLoaCovered(string id, DateTime eventStart);
    }
}
