using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UKSFWebsite.Api.Interfaces.Data.Cached;
using UKSFWebsite.Api.Models.Command;
using UKSFWebsite.Api.Models.Personnel;

namespace UKSFWebsite.Api.Interfaces.Personnel {
    public interface ILoaService : IDataBackedService<ILoaDataService> {
        IEnumerable<Loa> Get(List<string> ids);
        Task<string> Add(CommandRequestLoa requestBase);
        Task SetLoaState(string id, LoaReviewState state);
        bool IsLoaCovered(string id, DateTime eventStart);
    }
}
