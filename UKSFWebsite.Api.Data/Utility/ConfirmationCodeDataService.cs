using MongoDB.Driver;
using UKSFWebsite.Api.Interfaces.Data;
using UKSFWebsite.Api.Models.Utility;

namespace UKSFWebsite.Api.Data.Utility {
    public class ConfirmationCodeDataService : DataService<ConfirmationCode>, IConfirmationCodeDataService {
        public ConfirmationCodeDataService(IMongoDatabase database) : base(database, "confirmationCodes") { }
    }
}
