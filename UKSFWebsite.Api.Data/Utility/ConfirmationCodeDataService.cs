using MongoDB.Driver;
using UKSFWebsite.Api.Interfaces.Data;
using UKSFWebsite.Api.Interfaces.Events;
using UKSFWebsite.Api.Models.Utility;

namespace UKSFWebsite.Api.Data.Utility {
    public class ConfirmationCodeDataService : DataService<ConfirmationCode>, IConfirmationCodeDataService {
        public ConfirmationCodeDataService(IMongoDatabase database, IEventBus dataEventBus) : base(database, dataEventBus, "confirmationCodes") { }
    }
}
