using System.Threading.Tasks;
using UKSFWebsite.Api.Models;

namespace UKSFWebsite.Api.Services.Abstraction {
    public interface IConfirmationCodeService : IDataService<ConfirmationCode> {
        Task<string> CreateConfirmationCode(string value, bool steam = false);
        Task<string> GetConfirmationCode(string id);
    }
}
