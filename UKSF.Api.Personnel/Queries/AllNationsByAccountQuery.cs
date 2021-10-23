using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UKSF.Api.Personnel.Context;

namespace UKSF.Api.Personnel.Queries
{
    public interface IAllNationsByAccountQuery
    {
        Task<List<string>> ExecuteAsync();
    }

    public class AllNationsByAccountQuery : IAllNationsByAccountQuery
    {
        private readonly IAccountContext _accountContext;

        public AllNationsByAccountQuery(IAccountContext accountContext)
        {
            _accountContext = accountContext;
        }

        public Task<List<string>> ExecuteAsync()
        {
            var nations = _accountContext.Get()
                                         .Select(x => x.Nation)
                                         .Where(x => !string.IsNullOrWhiteSpace(x))
                                         .GroupBy(x => x)
                                         .OrderByDescending(x => x.Count())
                                         .ThenBy(x => x.Key)
                                         .Select(x => x.Key)
                                         .ToList();
            return Task.FromResult(nations);
        }
    }
}
