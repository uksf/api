using UKSF.Api.Shared.Context.Base;
using UKSF.Api.Shared.Events;
using UKSF.Api.Shared.Models;

namespace UKSF.Api.Shared.Context;

public interface IConfirmationCodeContext : IMongoContext<ConfirmationCode> { }

public class ConfirmationCodeContext : MongoContext<ConfirmationCode>, IConfirmationCodeContext
{
    public ConfirmationCodeContext(IMongoCollectionFactory mongoCollectionFactory, IEventBus eventBus) : base(
        mongoCollectionFactory,
        eventBus,
        "confirmationCodes"
    ) { }
}
