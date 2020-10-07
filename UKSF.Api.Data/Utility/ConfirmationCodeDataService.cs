using UKSF.Api.Interfaces.Data;
using UKSF.Api.Interfaces.Events;
using UKSF.Api.Models.Utility;

namespace UKSF.Api.Data.Utility {
    public class ConfirmationCodeDataService : DataService<ConfirmationCode>, IConfirmationCodeDataService {
        public ConfirmationCodeDataService(IDataCollectionFactory dataCollectionFactory, IDataEventBus<ConfirmationCode> dataEventBus) : base(dataCollectionFactory, dataEventBus, "confirmationCodes") { }
    }
}
