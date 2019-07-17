using System.Collections.Generic;
using MongoDB.Driver;
using UKSFWebsite.Api.Models;
using UKSFWebsite.Api.Services.Abstraction;

namespace UKSFWebsite.Api.Services.Data {
    public class RanksService : CachedDataService<Rank>, IRanksService {

        public RanksService(IMongoDatabase database) : base(database, "ranks") { }

        public override List<Rank> Get() {
            base.Get();
            Collection.Sort(Sort);
            return Collection;
        }
        
        public override Rank GetSingle(string name) => GetSingle(x => x.name == name);

        public int GetRankIndex(string rankName) {
            if (Collection == null) Get();
            return Collection.FindIndex(x => x.name == rankName);
        }

        public int Sort(string nameA, string nameB) {
            Rank rankA = GetSingle(nameA);
            Rank rankB = GetSingle(nameB);
            int rankOrderA = rankA?.order ?? 0;
            int rankOrderB = rankB?.order ?? 0;
            return rankOrderA < rankOrderB ? -1 : rankOrderA > rankOrderB ? 1 : 0;
        }

        public int Sort(Rank rankA, Rank rankB) {
            int rankOrderA = rankA?.order ?? 0;
            int rankOrderB = rankB?.order ?? 0;
            return rankOrderA < rankOrderB ? -1 : rankOrderA > rankOrderB ? 1 : 0;
        }

        public bool IsSuperior(string nameA, string nameB) {
            Rank rankA = GetSingle(nameA);
            Rank rankB = GetSingle(nameB);
            int rankOrderA = rankA?.order ?? 0;
            int rankOrderB = rankB?.order ?? 0;
            return rankOrderA < rankOrderB;
        }

        public bool IsEqual(string nameA, string nameB) {
            Rank rankA = GetSingle(nameA);
            Rank rankB = GetSingle(nameB);
            int rankOrderA = rankA?.order ?? 0;
            int rankOrderB = rankB?.order ?? 0;
            return rankOrderA == rankOrderB;
        }

        public bool IsSuperiorOrEqual(string nameA, string nameB) {
            Rank rankA = GetSingle(nameA);
            Rank rankB = GetSingle(nameB);
            int rankOrderA = rankA?.order ?? 0;
            int rankOrderB = rankB?.order ?? 0;
            return rankOrderA <= rankOrderB;
        }
    }

    public class RankComparer : IComparer<string> {
        private readonly IRanksService ranksService;
        public RankComparer(IRanksService ranksService) => this.ranksService = ranksService;

        public int Compare(string rankA, string rankB) => ranksService.Sort(rankA, rankB);
    }
}
