﻿using UKSF.Api.Core.Context;
using UKSF.Api.Core.Models;

namespace UKSF.Api.Core.Services;

public interface IDisplayNameService
{
    string GetDisplayName(DomainAccount domainAccount);
    string GetDisplayName(string id);
    string GetDisplayNameWithoutRank(DomainAccount domainAccount);
    string GetDisplayNameWithoutRank(string id);
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
        if (domainAccount is { MembershipState: MembershipState.SERVER })
        {
            return $"{domainAccount.Firstname} {domainAccount.Lastname}";
        }

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
        if (domainAccount is { MembershipState: MembershipState.SERVER })
        {
            return $"{domainAccount.Firstname} {domainAccount.Lastname}";
        }

        return string.IsNullOrEmpty(domainAccount?.Lastname) ? "Guest" : $"{domainAccount.Lastname}.{domainAccount.Firstname[0]}";
    }

    public string GetDisplayNameWithoutRank(string id)
    {
        var domainAccount = _accountContext.GetSingle(id);
        return domainAccount != null ? GetDisplayNameWithoutRank(domainAccount) : id;
    }
}