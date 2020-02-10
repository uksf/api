using UKSF.Api.Models.Personnel;

namespace UKSF.Api.Services.Common {
    public static class RankUtilities {
        public static int Sort(Rank rankA, Rank rankB) {
            int rankOrderA = rankA?.order ?? int.MaxValue;
            int rankOrderB = rankB?.order ?? int.MaxValue;
            return rankOrderA < rankOrderB ? -1 : rankOrderA > rankOrderB ? 1 : 0;
        }
    }
}
