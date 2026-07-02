using UKSF.Api.ArmaServer.Models;
using UKSF.Api.Core.Context.Base;
using UKSF.Api.Core.Events;
using UKSF.Api.Core.Services;

namespace UKSF.Api.ArmaServer.DataContext;

public interface ICampaignsContext : IMongoContext<DomainCampaign>, ICachedMongoContext;

public class CampaignsContext(IMongoCollectionFactory mongoCollectionFactory, IEventBus eventBus, IVariablesService variablesService)
    : CachedMongoContext<DomainCampaign>(mongoCollectionFactory, eventBus, variablesService, "campaigns"), ICampaignsContext;
