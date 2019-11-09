// ReSharper disable RedundantUsingDirective

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Linq;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using UKSFWebsite.Api.Models;
using UKSFWebsite.Api.Models.Accounts;
using UKSFWebsite.Api.Models.CommandRequests;
using UKSFWebsite.Api.Models.Logging;
using UKSFWebsite.Api.Services.Abstraction;
using UKSFWebsite.Api.Services.Data;

namespace UKSFWebsite.Api.Services.Utility {
    public class MigrationUtility {
        private const string KEY = "MIGRATED";
        private readonly IMongoDatabase database;
        private readonly IHostEnvironment currentEnvironment;

        public MigrationUtility(IMongoDatabase database, IHostEnvironment currentEnvironment) {
            this.database = database;
            this.currentEnvironment = currentEnvironment;
        }

        public void Migrate() {
            bool migrated = true;
            if (!currentEnvironment.IsDevelopment()) {
                string migratedString = VariablesWrapper.VariablesService().GetSingle(KEY).AsString();
                migrated = bool.Parse(migratedString);
            }

            // ReSharper disable once ConditionIsAlwaysTrueOrFalse
            if (!migrated) {
                try {
                    ExecuteMigration();
                    LogWrapper.AuditLog("SERVER", "Migration utility successfully ran");
                } catch (Exception e) {
                    LogWrapper.Log(e);
                } finally {
                    VariablesWrapper.VariablesService().Update(KEY, "true");
                }
            }
        }

        // TODO: CHECK BEFORE RELEASE
        private static void ExecuteMigration() {
            IUnitsService unitsService = ServiceWrapper.ServiceProvider.GetService<IUnitsService>();
            IRolesService rolesService = ServiceWrapper.ServiceProvider.GetService<IRolesService>();
            List<Role> roles = rolesService.Get(x => x.roleType == RoleType.UNIT);

            foreach (Unit unit in unitsService.Get()) {
                Dictionary<string, string> unitRoles = unit.roles;
                int originalCount = unit.roles.Count;
                foreach ((string key, string _) in unitRoles.ToList()) {
                    if (roles.All(x => x.name != key)) {
                        unitRoles.Remove(key);
                    }
                }

                if (roles.Count != originalCount) {
                    unitsService.Update(unit.id, Builders<Unit>.Update.Set(x => x.roles, unitRoles)).Wait();
                }
            }
        }
    }

//    public class OldLoa {
//        public bool approved;
//        [BsonId, BsonRepresentation(BsonType.ObjectId)] public string id;
//        public bool late;
//        public string reason;
//        public string emergency;
//        [BsonRepresentation(BsonType.ObjectId)] public string recipient;
//        public DateTime start;
//        public DateTime end;
//        public DateTime submitted;
//    }
}
