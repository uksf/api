using UKSF.Api.Base.Context;
using UKSF.Api.Base.Events;
using UKSF.Api.Personnel.Models;

namespace UKSF.Api.Personnel.Context {
    public interface IConfirmationCodeDataService : IDataService<ConfirmationCode> { }

    public class ConfirmationCodeDataService : DataService<ConfirmationCode>, IConfirmationCodeDataService {
        public ConfirmationCodeDataService(IDataCollectionFactory dataCollectionFactory, IDataEventBus<ConfirmationCode> dataEventBus) : base(dataCollectionFactory, dataEventBus, "confirmationCodes") { }
    }
}
