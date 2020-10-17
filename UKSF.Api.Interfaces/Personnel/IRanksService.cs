using UKSF.Api.Interfaces.Data.Cached;

namespace UKSF.Api.Interfaces.Personnel {
    public interface IRanksService : IDataBackedService<IRanksDataService> {
        int GetRankOrder(string rankName);
        int Sort(string nameA, string nameB);
        bool IsEqual(string nameA, string nameB);
        bool IsSuperior(string nameA, string nameB);
        bool IsSuperiorOrEqual(string nameA, string nameB);
    }
}
