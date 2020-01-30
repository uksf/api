using System.Collections.Generic;
using UKSF.Api.Models.Personnel;

namespace UKSF.Api.Interfaces.Data.Cached {
    public interface IRanksDataService : IDataService<Rank, IRanksDataService> {
        new List<Rank> Get();
        new Rank GetSingle(string name);
        int Sort(Rank rankA, Rank rankB);
    }
}
