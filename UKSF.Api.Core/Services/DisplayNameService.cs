using UKSF.Api.Core.Context;
using UKSF.Api.Core.Models;

namespace UKSF.Api.Core.Services;

public interface IDisplayNameService
{
    string GetDisplayName(string id);
    string GetDisplayName(DomainAccount domainAccount);
    string GetDisplayNameWithoutRank(string id);
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

    public string GetDisplayName(string id)
    {
        var domainAccount = _accountContext.GetSingle(id);

        return domainAccount switch
        {
            null => id,
            _    => GetDisplayName(domainAccount)
        };
    }

    public string GetDisplayName(DomainAccount domainAccount)
    {
        return domainAccount switch
        {
            _ when string.IsNullOrEmpty(domainAccount?.Lastname) => "Guest",
            { MembershipState: MembershipState.SERVER }          => FormatDisplayName(domainAccount.Lastname, domainAccount.Firstname),
            _                                                    => FormatDisplayName(domainAccount.Lastname, domainAccount.Firstname, domainAccount.Rank)
        };
    }

    public string GetDisplayNameWithoutRank(string id)
    {
        var domainAccount = _accountContext.GetSingle(id);

        return domainAccount switch
        {
            null => id,
            _    => GetDisplayNameWithoutRank(domainAccount)
        };
    }

    public string GetDisplayNameWithoutRank(DomainAccount domainAccount)
    {
        return domainAccount switch
        {
            _ when string.IsNullOrEmpty(domainAccount?.Lastname) => "Guest",
            { MembershipState: MembershipState.SERVER }          => FormatDisplayName(domainAccount.Lastname, domainAccount.Firstname),
            _                                                    => FormatDisplayName(domainAccount.Lastname, domainAccount.Firstname)
        };
    }

    private string FormatDisplayName(string lastName, string firstName, string rank = null)
    {
        if (!string.IsNullOrEmpty(rank))
        {
            var rankAbbreviation = _ranksContext.GetSingle(rank).Abbreviation;
            return $"{rankAbbreviation}.{lastName}.{firstName[0]}";
        }

        return $"{lastName}.{firstName[0]}";
    }
}
