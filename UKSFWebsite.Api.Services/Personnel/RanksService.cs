using System.Collections.Generic;
using UKSFWebsite.Api.Interfaces.Data.Cached;
using UKSFWebsite.Api.Interfaces.Personnel;
using UKSFWebsite.Api.Models.Personnel;

namespace UKSFWebsite.Api.Services.Personnel {
    public class RanksService : DataBackedService<IRanksDataService>, IRanksService {

        public RanksService(IRanksDataService data) : base(data) { }

        public int GetRankIndex(string rankName) {
            return Data().Get().FindIndex(x => x.name == rankName);
        }

        public int Sort(string nameA, string nameB) {
            Rank rankA = Data().GetSingle(nameA);
            Rank rankB = Data().GetSingle(nameB);
            return Data().Sort(rankA, rankB);
        }

        public bool IsSuperior(string nameA, string nameB) {
            Rank rankA = Data().GetSingle(nameA);
            Rank rankB = Data().GetSingle(nameB);
            int rankOrderA = rankA?.order ?? 0;
            int rankOrderB = rankB?.order ?? 0;
            return rankOrderA < rankOrderB;
        }

        public bool IsEqual(string nameA, string nameB) {
            Rank rankA = Data().GetSingle(nameA);
            Rank rankB = Data().GetSingle(nameB);
            int rankOrderA = rankA?.order ?? 0;
            int rankOrderB = rankB?.order ?? 0;
            return rankOrderA == rankOrderB;
        }

        public bool IsSuperiorOrEqual(string nameA, string nameB) => IsSuperior(nameA, nameB) || IsEqual(nameA, nameB);
    }

    public class RankComparer : IComparer<string> {
        private readonly IRanksService ranksService;
        public RankComparer(IRanksService ranksService) => this.ranksService = ranksService;

        public int Compare(string rankA, string rankB) => ranksService.Sort(rankA, rankB);
    }
}
