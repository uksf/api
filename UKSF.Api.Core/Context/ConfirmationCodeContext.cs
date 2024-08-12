using UKSF.Api.Core.Context.Base;
using UKSF.Api.Core.Events;
using UKSF.Api.Core.Models;

namespace UKSF.Api.Core.Context;

public interface IConfirmationCodeContext : IMongoContext<ConfirmationCode>;

public class ConfirmationCodeContext : MongoContext<ConfirmationCode>, IConfirmationCodeContext
{
    public ConfirmationCodeContext(IMongoCollectionFactory mongoCollectionFactory, IEventBus eventBus) : base(
        mongoCollectionFactory,
        eventBus,
        "confirmationCodes"
    ) { }
}
