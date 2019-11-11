using System.Threading.Tasks;
using UKSFWebsite.Api.Interfaces.Data;

namespace UKSFWebsite.Api.Interfaces.Utility {
    public interface IConfirmationCodeService : IDataBackedService<IConfirmationCodeDataService> {
        Task<string> CreateConfirmationCode(string value, bool integration = false);
        Task<string> GetConfirmationCode(string id);
    }
}
