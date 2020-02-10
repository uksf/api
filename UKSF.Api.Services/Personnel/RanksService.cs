using System.Collections.Generic;
using UKSF.Api.Interfaces.Data.Cached;
using UKSF.Api.Interfaces.Personnel;
using UKSF.Api.Models.Personnel;
using UKSF.Api.Services.Common;

namespace UKSF.Api.Services.Personnel {
    public class RanksService : IRanksService {
        private readonly IRanksDataService data;

        public RanksService(IRanksDataService data) => this.data = data;

        public IRanksDataService Data() => data;

        public int GetRankIndex(string rankName) {
            return data.Get().FindIndex(x => x.name == rankName);
        }

        public int Sort(string nameA, string nameB) {
            Rank rankA = data.GetSingle(nameA);
            Rank rankB = data.GetSingle(nameB);
            return RankUtilities.Sort(rankA, rankB);
        }

        public bool IsSuperior(string nameA, string nameB) {
            Rank rankA = data.GetSingle(nameA);
            Rank rankB = data.GetSingle(nameB);
            int rankOrderA = rankA?.order ?? int.MaxValue;
            int rankOrderB = rankB?.order ?? int.MaxValue;
            return rankOrderA < rankOrderB;
        }

        public bool IsEqual(string nameA, string nameB) {
            Rank rankA = data.GetSingle(nameA);
            Rank rankB = data.GetSingle(nameB);
            int rankOrderA = rankA?.order ?? int.MinValue;
            int rankOrderB = rankB?.order ?? int.MinValue;
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
