using UKSF.Api.Models.Personnel;

namespace UKSF.Api.Services.Common {
    public static class RankUtilities {
        public static int Sort(this Rank rankA, Rank rankB) {
            int rankOrderA = rankA?.order ?? 0;
            int rankOrderB = rankB?.order ?? 0;
            return rankOrderA < rankOrderB ? -1 : rankOrderA > rankOrderB ? 1 : 0;
        }
    }
}
