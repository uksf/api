using UKSF.Api.Shared.Context;
using UKSF.Api.Shared.Models;

namespace UKSF.Api.Shared.Services;

public interface IDisplayNameService
{
    string GetDisplayName(DomainAccount domainAccount);
    string GetDisplayName(string id);
    string GetDisplayNameWithoutRank(DomainAccount domainAccount);
}

public class DisplayNameService : IDisplayNameService
{
    private readonly IAccountContext _accountContext;
    private readonly IRanksContext _ranksContext;

    public DisplayNameService(IAccountContext accountContext, IRanksContext ranksContext)
    {
        _accountContext = accountContext;
        _ranksContext = ranksContext;
    }

    public string GetDisplayName(DomainAccount domainAccount)
    {
        var rank = domainAccount.Rank != null ? _ranksContext.GetSingle(domainAccount.Rank) : null;
        return rank == null
            ? $"{domainAccount.Lastname}.{domainAccount.Firstname[0]}"
            : $"{rank.Abbreviation}.{domainAccount.Lastname}.{domainAccount.Firstname[0]}";
    }

    public string GetDisplayName(string id)
    {
        var domainAccount = _accountContext.GetSingle(id);
        return domainAccount != null ? GetDisplayName(domainAccount) : id;
    }

    public string GetDisplayNameWithoutRank(DomainAccount domainAccount)
    {
        return string.IsNullOrEmpty(domainAccount?.Lastname) ? "Guest" : $"{domainAccount.Lastname}.{domainAccount.Firstname[0]}";
    }
}
