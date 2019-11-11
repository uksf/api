using UKSFWebsite.Api.Interfaces.Data.Cached;

namespace UKSFWebsite.Api.Interfaces.Personnel {
    public interface IRanksService {
        IRanksDataService Data();
        int GetRankIndex(string rankName);
        int Sort(string nameA, string nameB);
        bool IsEqual(string nameA, string nameB);
        bool IsSuperior(string nameA, string nameB);
        bool IsSuperiorOrEqual(string nameA, string nameB);
    }
}
