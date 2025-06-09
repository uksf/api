using UKSF.Api.Core.Context;
using UKSF.Api.Core.Models;
using UKSF.Api.Core.Models.Domain;

namespace UKSF.Api.Core.Services;

public interface IDisplayNameService
{
    string GetDisplayName(string id);
    string GetDisplayName(DomainAccount account);
    string GetDisplayNameWithoutRank(string id);
    string GetDisplayNameWithoutRank(DomainAccount account);
}

public class DisplayNameService(IAccountContext accountContext, IRanksContext ranksContext) : IDisplayNameService
{
    public string GetDisplayName(string id)
    {
        var account = accountContext.GetSingle(id);

        return account switch
        {
            null => id,
            _    => GetDisplayName(account)
        };
    }

    public string GetDisplayName(DomainAccount account)
    {
        return account switch
        {
            _ when string.IsNullOrEmpty(account?.Lastname) => "Guest",
            { MembershipState: MembershipState.Server }    => FormatDisplayName(account.Lastname, account.Firstname),
            _                                              => FormatDisplayName(account.Lastname, account.Firstname, account.Rank)
        };
    }

    public string GetDisplayNameWithoutRank(string id)
    {
        var account = accountContext.GetSingle(id);

        return account switch
        {
            null => id,
            _    => GetDisplayNameWithoutRank(account)
        };
    }

    public string GetDisplayNameWithoutRank(DomainAccount account)
    {
        return account switch
        {
            _ when string.IsNullOrEmpty(account?.Lastname) => "Guest",
            { MembershipState: MembershipState.Server }    => FormatDisplayName(account.Lastname, account.Firstname),
            _                                              => FormatDisplayName(account.Lastname, account.Firstname)
        };
    }

    private string FormatDisplayName(string lastName, string firstName, string rank = null)
    {
        if (!string.IsNullOrEmpty(rank))
        {
            var rankAbbreviation = ranksContext.GetSingle(rank).Abbreviation;
            return $"{rankAbbreviation}.{lastName}.{firstName[0]}";
        }

        return $"{lastName}.{firstName[0]}";
    }
}
