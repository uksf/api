using System;
using Microsoft.Extensions.Hosting;
using UKSF.Api.Admin.Extensions;
using UKSF.Api.Admin.Services;
using UKSF.Api.Shared.Events;

namespace UKSF.Api.Services {
    public class MigrationUtility {
        private const string KEY = "MIGRATED";
        private readonly IHostEnvironment currentEnvironment;
        private readonly ILogger logger;
        private readonly IVariablesService variablesService;

        public MigrationUtility(IHostEnvironment currentEnvironment, IVariablesService variablesService, ILogger logger) {
            this.currentEnvironment = currentEnvironment;
            this.variablesService = variablesService;
            this.logger = logger;
        }

        public void Migrate() {
            bool migrated = true;
            if (!currentEnvironment.IsDevelopment()) {
                string migratedString = variablesService.GetVariable(KEY).AsString();
                migrated = bool.Parse(migratedString);
            }

            // ReSharper disable once ConditionIsAlwaysTrueOrFalse
            if (!migrated) {
                try {
                    ExecuteMigration();
                    logger.LogAudit("Migration utility successfully ran");
                } catch (Exception e) {
                    logger.LogError(e);
                } finally {
                    variablesService.Data.Update(KEY, "true");
                }
            }
        }

        // TODO: CHECK BEFORE RELEASE
        private static void ExecuteMigration() { }
    }
}
