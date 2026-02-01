using UKSF.Api.Core.Context.Base;
using UKSF.Api.Core.Events;
using UKSF.Api.Core.Services;
using UKSF.Api.Modpack.Models;

namespace UKSF.Api.Modpack.Context;

public interface IWorkshopModsContext : IMongoContext<DomainWorkshopMod>, ICachedMongoContext { }

public class WorkshopModsContext(IMongoCollectionFactory mongoCollectionFactory, IEventBus eventBus, IVariablesService variablesService)
    : CachedMongoContext<DomainWorkshopMod>(mongoCollectionFactory, eventBus, variablesService, "workshopMods"), IWorkshopModsContext
{
    protected override IEnumerable<DomainWorkshopMod> OrderCollection(IEnumerable<DomainWorkshopMod> collection)
    {
        return collection.OrderBy(x => x.Name);
    }
}
