﻿using UKSF.Api.Personnel.Models;

namespace UKSF.Api.Personnel.Services {
    public interface IDisplayNameService {
        string GetDisplayName(Account account);
        string GetDisplayName(string id);
        string GetDisplayNameWithoutRank(Account account);
    }

    public class DisplayNameService : IDisplayNameService {
        private readonly IAccountService accountService;
        private readonly IRanksService ranksService;

        public DisplayNameService(IRanksService ranksService, IAccountService accountService) {
            this.ranksService = ranksService;
            this.accountService = accountService;
        }

        public string GetDisplayName(Account account) {
            Rank rank = account.rank != null ? ranksService.Data.GetSingle(account.rank) : null;
            return rank == null ? $"{account.lastname}.{account.firstname[0]}" : $"{rank.abbreviation}.{account.lastname}.{account.firstname[0]}";
        }

        public string GetDisplayName(string id) {
            Account account = accountService.Data.GetSingle(id);
            return account != null ? GetDisplayName(account) : id;
        }

        public string GetDisplayNameWithoutRank(Account account) => string.IsNullOrEmpty(account?.lastname) ? "Guest" : $"{account.lastname}.{account.firstname[0]}";
    }
}