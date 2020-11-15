using UKSF.Api.Base.Context;
using UKSF.Api.Personnel.Models;
using UKSF.Api.Shared.Context;
using UKSF.Api.Shared.Events;

namespace UKSF.Api.Personnel.Context {
    public interface IConfirmationCodeDataService : IDataService<ConfirmationCode> { }

    public class ConfirmationCodeDataService : DataService<ConfirmationCode>, IConfirmationCodeDataService {
        public ConfirmationCodeDataService(IDataCollectionFactory dataCollectionFactory, IDataEventBus<ConfirmationCode> dataEventBus) : base(dataCollectionFactory, dataEventBus, "confirmationCodes") { }
    }
}
