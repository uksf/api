using System;
using System.Collections.Generic;
using Microsoft.Extensions.Hosting;
using UKSF.Api.Admin.Context;
using UKSF.Api.Admin.Extensions;
using UKSF.Api.Admin.Services;
using UKSF.Api.Shared.Context;
using UKSF.Api.Shared.Events;
using UKSF.Api.Shared.Models;

namespace UKSF.Api.Services {
    public class MigrationUtility {
        private const string KEY = "MIGRATED";
        private readonly IHostEnvironment _currentEnvironment;
        private readonly ILogger _logger;
        private readonly ILogContext _logContext;
        private readonly IHttpErrorLogContext _httpErrorLogContext;
        private readonly IAuditLogContext _auditLogContext;
        private readonly ILauncherLogContext _launcherLogContext;
        private readonly IVariablesContext _variablesContext;
        private readonly IVariablesService _variablesService;

        public MigrationUtility(IHostEnvironment currentEnvironment, IVariablesService variablesService, IVariablesContext variablesContext, ILogger logger, ILogContext logContext, IHttpErrorLogContext httpErrorLogContext, IAuditLogContext auditLogContext, ILauncherLogContext launcherLogContext) {
            _currentEnvironment = currentEnvironment;
            _variablesService = variablesService;
            _variablesContext = variablesContext;
            _logger = logger;
            _logContext = logContext;
            _httpErrorLogContext = httpErrorLogContext;
            _auditLogContext = auditLogContext;
            _launcherLogContext = launcherLogContext;
        }

        public void Migrate() {
            bool migrated = false;
            if (!_currentEnvironment.IsDevelopment()) {
                string migratedString = _variablesService.GetVariable(KEY).AsString();
                migrated = bool.Parse(migratedString);
            }

            // ReSharper disable once ConditionIsAlwaysTrueOrFalse
            if (!migrated) {
                try {
                    ExecuteMigration();
                    _logger.LogAudit("Migration utility successfully ran");
                } catch (Exception e) {
                    _logger.LogError(e);
                } finally {
                    _variablesContext.Update(KEY, "true");
                }
            }
        }

        // TODO: CHECK BEFORE RELEASE
        private void ExecuteMigration() {
            IEnumerable<BasicLog> logs = _logContext.Get();
            foreach (BasicLog log in logs) {
                _logContext.Replace(log);
            }

            IEnumerable<HttpErrorLog> errorLogs = _httpErrorLogContext.Get();
            foreach (HttpErrorLog log in errorLogs) {
                _httpErrorLogContext.Replace(log);
            }

            IEnumerable<AuditLog> auditLogs = _auditLogContext.Get();
            foreach (AuditLog log in auditLogs) {
                _auditLogContext.Replace(log);
            }

            IEnumerable<LauncherLog> launcherLogs = _launcherLogContext.Get();
            foreach (LauncherLog log in launcherLogs) {
                _launcherLogContext.Replace(log);
            }
        }
    }
}
