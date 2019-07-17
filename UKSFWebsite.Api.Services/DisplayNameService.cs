using UKSFWebsite.Api.Models;
using UKSFWebsite.Api.Models.Accounts;
using UKSFWebsite.Api.Services.Abstraction;

namespace UKSFWebsite.Api.Services {
    public class DisplayNameService : IDisplayNameService {
        private readonly IAccountService accountService;
        private readonly IRanksService ranksService;

        public DisplayNameService(IRanksService ranksService, IAccountService accountService) {
            this.ranksService = ranksService;
            this.accountService = accountService;
        }

        public string GetDisplayName(Account account) {
            Rank rank = account.rank != null ? ranksService.GetSingle(account.rank) : null;
            if (account.membershipState == MembershipState.MEMBER) {
                return rank == null ? account.lastname + "." + account.firstname[0] : rank.abbreviation + "." + account.lastname + "." + account.firstname[0];
            }

            return $"{(rank != null ? $"{rank.abbreviation}." : "")}{account.lastname}.{account.firstname[0]}";
        }

        public string GetDisplayName(string id) {
            Account account = accountService.GetSingle(id);
            return account != null ? GetDisplayName(account) : id;
        }

        public string GetDisplayNameWithoutRank(Account account) => string.IsNullOrEmpty(account.lastname) ? "Guest" : $"{account.lastname}.{account.firstname[0]}";
    }
}
