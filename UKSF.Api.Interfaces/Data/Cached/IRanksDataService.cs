using System.Collections.Generic;
using UKSF.Api.Models.Personnel;

namespace UKSF.Api.Interfaces.Data.Cached {
    public interface IRanksDataService : IDataService<Rank, IRanksDataService>, ICachedDataService {
        new List<Rank> Get();
        new Rank GetSingle(string name);
    }
}
