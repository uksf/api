using System;
using UKSF.Api.Personnel.Context;
using UKSF.Api.Shared.Context;
using UKSF.Api.Shared.Events;

namespace UKSF.Api.Services
{
    public class MigrationUtility
    {
        private readonly IAccountContext _accountContext;
        private readonly ILogger _logger;
        private readonly IMigrationContext _migrationContext;
        private const int Version = 0;

        public MigrationUtility(IMigrationContext migrationContext, ILogger logger, IAccountContext accountContext)
        {
            _migrationContext = migrationContext;
            _logger = logger;
            _accountContext = accountContext;
        }

        public void Migrate()
        {
            if (_migrationContext.GetSingle(x => x.Version == Version) != null)
            {
                return;
            }

            try
            {
                ExecuteMigration();
                _migrationContext.Add(new() { Version = Version });
                _logger.LogAudit($"Migration version {Version} executed successfully");
            }
            catch (Exception e)
            {
                _logger.LogError(e);
                throw;
            }
        }

        private void ExecuteMigration()
        {
            var accounts = _accountContext.Get();
            foreach (var account in accounts)
            {
                account.Qualifications = new();
                _accountContext.Replace(account);
            }
        }
    }
}
