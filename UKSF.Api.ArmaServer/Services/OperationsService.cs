using UKSF.Api.ArmaServer.DataContext;
using UKSF.Api.ArmaServer.Models;

namespace UKSF.Api.ArmaServer.Services;

public interface IOperationsService
{
    Task DeleteOperation(string id);
}

public class OperationsService(
    IOperationsContext operationsContext,
    ICampaignMissionsContext campaignMissionsContext,
    ICampaignMissionsService campaignMissionsService,
    IIntelPagesContext intelPagesContext
) : IOperationsService
{
    public async Task DeleteOperation(string id)
    {
        foreach (var mission in campaignMissionsContext.Get(x => x.OperationId == id).ToList())
        {
            await campaignMissionsService.DeleteMission(mission.Id);
        }

        await intelPagesContext.DeleteMany(x => x.Scope == IntelScope.Operation && x.OwnerId == id);
        await operationsContext.Delete(id);
    }
}
