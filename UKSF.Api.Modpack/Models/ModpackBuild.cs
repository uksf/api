using System;
using System.Collections.Generic;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.Base.Models;

namespace UKSF.Api.Modpack.Models {
    public class ModpackBuild : DatabaseObject {
        [BsonRepresentation(BsonType.ObjectId)] public string BuilderId;
        public int BuildNumber;
        public ModpackBuildResult BuildResult = ModpackBuildResult.NONE;
        public GithubCommit Commit;
        public bool Finished;
        public bool IsRebuild;
        public GameEnvironment Environment;
        public bool Running;
        public List<ModpackBuildStep> Steps = new List<ModpackBuildStep>();
        public DateTime StartTime = DateTime.Now;
        public DateTime EndTime = DateTime.Now;
        public string Version;
        public Dictionary<string, object> EnvironmentVariables = new Dictionary<string, object>();
    }
}
