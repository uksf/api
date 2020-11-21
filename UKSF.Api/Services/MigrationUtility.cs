using System;
using Microsoft.Extensions.Hosting;
using UKSF.Api.Admin.Context;
using UKSF.Api.Admin.Extensions;
using UKSF.Api.Admin.Services;
using UKSF.Api.Shared.Events;

namespace UKSF.Api.Services {
    public class MigrationUtility {
        private const string KEY = "MIGRATED";
        private readonly IHostEnvironment _currentEnvironment;
        private readonly ILogger _logger;
        private readonly IVariablesContext _variablesContext;
        private readonly IVariablesService _variablesService;

        public MigrationUtility(IHostEnvironment currentEnvironment, IVariablesService variablesService, IVariablesContext variablesContext, ILogger logger) {
            _currentEnvironment = currentEnvironment;
            _variablesService = variablesService;
            _variablesContext = variablesContext;
            _logger = logger;
        }

        public void Migrate() {
            bool migrated = true;
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
        private static void ExecuteMigration() { }
    }
}
