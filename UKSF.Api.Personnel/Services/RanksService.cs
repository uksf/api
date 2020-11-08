using System.Collections.Generic;
using UKSF.Api.Base.Context;
using UKSF.Api.Personnel.Context;
using UKSF.Api.Personnel.Models;

namespace UKSF.Api.Personnel.Services {
    public interface IRanksService : IDataBackedService<IRanksDataService> {
        int GetRankOrder(string rankName);
        int Sort(string nameA, string nameB);
        bool IsEqual(string nameA, string nameB);
        bool IsSuperior(string nameA, string nameB);
        bool IsSuperiorOrEqual(string nameA, string nameB);
    }

    public class RanksService : DataBackedService<IRanksDataService>, IRanksService {

        public RanksService(IRanksDataService data) : base(data) { }

        public int GetRankOrder(string rankName) => Data.GetSingle(rankName)?.order ?? -1;

        public int Sort(string nameA, string nameB) {
            Rank rankA = Data.GetSingle(nameA);
            Rank rankB = Data.GetSingle(nameB);
            int rankOrderA = rankA?.order ?? int.MaxValue;
            int rankOrderB = rankB?.order ?? int.MaxValue;
            return rankOrderA < rankOrderB ? -1 : rankOrderA > rankOrderB ? 1 : 0;
        }

        public bool IsSuperior(string nameA, string nameB) {
            Rank rankA = Data.GetSingle(nameA);
            Rank rankB = Data.GetSingle(nameB);
            int rankOrderA = rankA?.order ?? int.MaxValue;
            int rankOrderB = rankB?.order ?? int.MaxValue;
            return rankOrderA < rankOrderB;
        }

        public bool IsEqual(string nameA, string nameB) {
            Rank rankA = Data.GetSingle(nameA);
            Rank rankB = Data.GetSingle(nameB);
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
