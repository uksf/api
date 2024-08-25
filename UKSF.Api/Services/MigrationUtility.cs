using UKSF.Api.Core;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Context.Base;
using UKSF.Api.Core.Models;
using UKSF.Api.Core.Models.Domain;

namespace UKSF.Api.Services;

public class MigrationUtility(IMigrationContext migrationContext, IUksfLogger logger, IServiceProvider serviceProvider)
{
    private const int Version = 1;

    public void Migrate()
    {
        if (migrationContext.GetSingle(x => x.Version == Version) is not null)
        {
            return;
        }

        try
        {
            ExecuteMigration();
            migrationContext.Add(new Migration { Version = Version });
            logger.LogInfo($"Migration version {Version} executed successfully");
        }
        catch (Exception e)
        {
            logger.LogError(e);
            throw;
        }
    }

    private void ExecuteMigration()
    {
        var context = serviceProvider.GetRequiredService<IMongoCollectionFactory>().CreateMongoCollection<DomainCommandRequest>("commandRequests");
        var commandRequestContext = serviceProvider.GetRequiredService<ICommandRequestContext>();
        var commandRequestLoas = context.Get().Where(x => x.Type == "Loa").ToList();
        context.DeleteManyAsync(x => commandRequestLoas.Any(y => y.Id == x.Id)).GetAwaiter().GetResult();
        Task.WhenAll(commandRequestLoas.Select(commandRequestContext.Add)).GetAwaiter().GetResult();

        var archiveContext = serviceProvider.GetRequiredService<IMongoCollectionFactory>()
                                            .CreateMongoCollection<DomainCommandRequest>("commandRequestsArchive");
        var commandRequestArchiveContext = serviceProvider.GetRequiredService<ICommandRequestArchiveContext>();
        var archivedCommandRequestLoas = archiveContext.Get().ToList().Where(x => x.Type == "Loa").ToList();
        archiveContext.DeleteManyAsync(x => archivedCommandRequestLoas.Any(y => y.Id == x.Id)).GetAwaiter().GetResult();
        Task.WhenAll(archivedCommandRequestLoas.Select(commandRequestArchiveContext.Add)).GetAwaiter().GetResult();
    }
}
