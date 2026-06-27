using UKSF.Api.ArmaServer.Models;
using UKSF.Api.Core.Context.Base;
using UKSF.Api.Core.Events;

namespace UKSF.Api.ArmaServer.DataContext;

public interface ICampaignsContext : IMongoContext<DomainCampaign>;

public class CampaignsContext(IMongoCollectionFactory mongoCollectionFactory, IEventBus eventBus)
    : MongoContext<DomainCampaign>(mongoCollectionFactory, eventBus, "campaigns"), ICampaignsContext;
