using System;
using System.Threading.Tasks;
using UKSF.Api.Interfaces.Data;
using UKSF.Api.Models.Utility;

namespace UKSF.Api.Interfaces.Utility {
    public interface IConfirmationCodeService : IDataBackedService<IConfirmationCodeDataService> {
        Task<string> CreateConfirmationCode(string value);
        Task<string> GetConfirmationCode(string id);
        Task ClearConfirmationCodes(Func<ConfirmationCode, bool> predicate);
    }
}
