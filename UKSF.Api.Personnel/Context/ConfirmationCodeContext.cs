using UKSF.Api.Base.Context;
using UKSF.Api.Personnel.Models;
using UKSF.Api.Shared.Context;
using UKSF.Api.Shared.Events;

namespace UKSF.Api.Personnel.Context {
    public interface IConfirmationCodeContext : IMongoContext<ConfirmationCode> { }

    public class ConfirmationCodeContext : MongoContext<ConfirmationCode>, IConfirmationCodeContext {
        public ConfirmationCodeContext(IMongoCollectionFactory mongoCollectionFactory, IDataEventBus<ConfirmationCode> dataEventBus) :
            base(mongoCollectionFactory, dataEventBus, "confirmationCodes") { }
    }
}
