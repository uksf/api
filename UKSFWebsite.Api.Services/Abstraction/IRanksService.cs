using System.Collections.Generic;
using UKSFWebsite.Api.Models;

namespace UKSFWebsite.Api.Services.Abstraction {
    public interface IRanksService : IDataService<Rank> {
        new List<Rank> Get();
        new Rank GetSingle(string name);
        int GetRankIndex(string rankName);
        bool IsEqual(string nameA, string nameB);
        bool IsSuperior(string nameA, string nameB);
        bool IsSuperiorOrEqual(string nameA, string nameB);
        int Sort(string nameA, string nameB);
        int Sort(Rank rankA, Rank rankB);
    }
}
