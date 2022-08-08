using UKSF.Api.Personnel.Context;

namespace UKSF.Api.Personnel.Services;

public interface IRanksService
{
    int GetRankOrder(string rankName);
    int Sort(string nameA, string nameB);
    bool IsEqual(string nameA, string nameB);
    bool IsSuperior(string nameA, string nameB);
    bool IsSuperiorOrEqual(string nameA, string nameB);
}

public class RanksService : IRanksService
{
    private readonly IRanksContext _ranksContext;

    public RanksService(IRanksContext ranksContext)
    {
        _ranksContext = ranksContext;
    }

    public int GetRankOrder(string rankName)
    {
        return _ranksContext.GetSingle(rankName)?.Order ?? -1;
    }

    public int Sort(string nameA, string nameB)
    {
        var rankA = _ranksContext.GetSingle(nameA);
        var rankB = _ranksContext.GetSingle(nameB);
        var rankOrderA = rankA?.Order ?? int.MaxValue;
        var rankOrderB = rankB?.Order ?? int.MaxValue;
        return rankOrderA < rankOrderB ? -1 :
            rankOrderA > rankOrderB    ? 1 : 0;
    }

    public bool IsSuperior(string nameA, string nameB)
    {
        var rankA = _ranksContext.GetSingle(nameA);
        var rankB = _ranksContext.GetSingle(nameB);
        var rankOrderA = rankA?.Order ?? int.MaxValue;
        var rankOrderB = rankB?.Order ?? int.MaxValue;
        return rankOrderA < rankOrderB;
    }

    public bool IsEqual(string nameA, string nameB)
    {
        var rankA = _ranksContext.GetSingle(nameA);
        var rankB = _ranksContext.GetSingle(nameB);
        var rankOrderA = rankA?.Order ?? int.MinValue;
        var rankOrderB = rankB?.Order ?? int.MinValue;
        return rankOrderA == rankOrderB;
    }

    public bool IsSuperiorOrEqual(string nameA, string nameB)
    {
        return IsSuperior(nameA, nameB) || IsEqual(nameA, nameB);
    }
}

public class RankComparer : IComparer<string>
{
    private readonly IRanksService _ranksService;

    public RankComparer(IRanksService ranksService)
    {
        _ranksService = ranksService;
    }

    public int Compare(string rankA, string rankB)
    {
        return _ranksService.Sort(rankA, rankB);
    }
}
