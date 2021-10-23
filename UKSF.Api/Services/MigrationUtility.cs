using System;
using Microsoft.Extensions.Hosting;
using UKSF.Api.Admin.Context;
using UKSF.Api.Admin.Extensions;
using UKSF.Api.Admin.Services;
using UKSF.Api.Shared.Context;
using UKSF.Api.Shared.Events;

namespace UKSF.Api.Services
{
    public class MigrationUtility
    {
        private const string KEY = "MIGRATED";
        private readonly IAuditLogContext _auditLogContext;
        private readonly IHostEnvironment _currentEnvironment;
        private readonly IErrorLogContext _errorLogContext;
        private readonly ILauncherLogContext _launcherLogContext;
        private readonly ILogContext _logContext;
        private readonly ILogger _logger;
        private readonly IVariablesContext _variablesContext;
        private readonly IVariablesService _variablesService;

        public MigrationUtility(
            IHostEnvironment currentEnvironment,
            IVariablesService variablesService,
            IVariablesContext variablesContext,
            ILogger logger,
            ILogContext logContext,
            IErrorLogContext errorLogContext,
            IAuditLogContext auditLogContext,
            ILauncherLogContext launcherLogContext
        )
        {
            _currentEnvironment = currentEnvironment;
            _variablesService = variablesService;
            _variablesContext = variablesContext;
            _logger = logger;
            _logContext = logContext;
            _errorLogContext = errorLogContext;
            _auditLogContext = auditLogContext;
            _launcherLogContext = launcherLogContext;
        }

        public void Migrate()
        {
            var migrated = true;
            if (!_currentEnvironment.IsDevelopment())
            {
                migrated = _variablesService.GetVariable(KEY).AsBool();
            }

            if (!migrated)
            {
                try
                {
                    ExecuteMigration();
                    _logger.LogAudit("Migration utility successfully ran");
                }
                catch (Exception e)
                {
                    _logger.LogError(e);
                }
                finally
                {
                    _variablesContext.Update(KEY, "true");
                }
            }
        }

        // TODO: CHECK BEFORE RELEASE
        private void ExecuteMigration()
        {
            var logs = _logContext.Get();
            foreach (var log in logs)
            {
                _logContext.Replace(log);
            }

            var errorLogs = _errorLogContext.Get();
            foreach (var log in errorLogs)
            {
                _errorLogContext.Replace(log);
            }

            var auditLogs = _auditLogContext.Get();
            foreach (var log in auditLogs)
            {
                _auditLogContext.Replace(log);
            }

            var launcherLogs = _launcherLogContext.Get();
            foreach (var log in launcherLogs)
            {
                _launcherLogContext.Replace(log);
            }
        }
    }
}
