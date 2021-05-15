using UKSF.Api.Personnel.Context;
using UKSF.Api.Personnel.Models;

namespace UKSF.Api.Personnel.Services
{
    public interface IDisplayNameService
    {
        string GetDisplayName(Account account);
        string GetDisplayName(string id);
        string GetDisplayNameWithoutRank(Account account);
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

        public string GetDisplayName(Account account)
        {
            Rank rank = account.Rank != null ? _ranksContext.GetSingle(account.Rank) : null;
            return rank == null ? $"{account.Lastname}.{account.Firstname[0]}" : $"{rank.Abbreviation}.{account.Lastname}.{account.Firstname[0]}";
        }

        public string GetDisplayName(string id)
        {
            Account account = _accountContext.GetSingle(id);
            return account != null ? GetDisplayName(account) : id;
        }

        public string GetDisplayNameWithoutRank(Account account)
        {
            return string.IsNullOrEmpty(account?.Lastname) ? "Guest" : $"{account.Lastname}.{account.Firstname[0]}";
        }
    }
}
