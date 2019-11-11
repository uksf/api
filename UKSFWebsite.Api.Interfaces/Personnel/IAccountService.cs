using System.Threading.Tasks;
using MongoDB.Driver;
using UKSFWebsite.Api.Interfaces.Data.Cached;
using UKSFWebsite.Api.Models.Personnel;

namespace UKSFWebsite.Api.Interfaces.Personnel {
    public interface IAccountService : IDataBackedService<IAccountDataService> {
        Task Update(string id, string fieldName, object value);
        Task Update(string id, UpdateDefinition<Account> update);
    }
}
