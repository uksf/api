using System.Collections.Generic;
using UKSFWebsite.Api.Models.Personnel;

namespace UKSFWebsite.Api.Interfaces.Data.Cached {
    public interface IRanksDataService : IDataService<Rank, IRanksDataService> {
        new List<Rank> Get();
        new Rank GetSingle(string name);
        int Sort(Rank rankA, Rank rankB);
    }
}
