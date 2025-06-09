using UKSF.Api.Core.Context.Base;
using UKSF.Api.Core.Events;
using UKSF.Api.Core.Models.Domain;

namespace UKSF.Api.Core.Context;

public interface IConfirmationCodeContext : IMongoContext<DomainConfirmationCode>;

public class ConfirmationCodeContext : MongoContext<DomainConfirmationCode>, IConfirmationCodeContext
{
    public ConfirmationCodeContext(IMongoCollectionFactory mongoCollectionFactory, IEventBus eventBus) : base(
        mongoCollectionFactory,
        eventBus,
        "confirmationCodes"
    ) { }
}
