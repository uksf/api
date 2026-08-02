using UKSF.Api.Backups.Models;
using UKSF.Api.Core.Context.Base;
using UKSF.Api.Core.Events;

namespace UKSF.Api.Backups.DataContext;

public interface IBackupRunsContext : IMongoContext<DomainBackupRun>;

public class BackupRunsContext(IMongoCollectionFactory mongoCollectionFactory, IEventBus eventBus)
    : MongoContext<DomainBackupRun>(mongoCollectionFactory, eventBus, "backupRuns"), IBackupRunsContext;
