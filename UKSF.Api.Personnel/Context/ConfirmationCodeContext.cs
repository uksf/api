using UKSF.Api.Base.Context;
using UKSF.Api.Base.Events;
using UKSF.Api.Personnel.Models;
using UKSF.Api.Shared.Context;
using UKSF.Api.Shared.Events;

namespace UKSF.Api.Personnel.Context {
    public interface IConfirmationCodeContext : IMongoContext<ConfirmationCode> { }

    public class ConfirmationCodeContext : MongoContext<ConfirmationCode>, IConfirmationCodeContext {
        public ConfirmationCodeContext(IMongoCollectionFactory mongoCollectionFactory, IEventBus eventBus) :
            base(mongoCollectionFactory, eventBus, "confirmationCodes") { }
    }
}
