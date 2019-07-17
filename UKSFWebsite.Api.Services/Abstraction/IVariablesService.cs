using System.Threading.Tasks;
using UKSFWebsite.Api.Models;

namespace UKSFWebsite.Api.Services.Abstraction {
    public interface IVariablesService : IDataService<VariableItem> {
        Task Update(string key, object value);
    }
}