using System;
using System.Collections.Generic;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.Base.Models;

namespace UKSF.Api.Modpack.Models
{
    public class ModpackBuild : MongoObject
    {
        [BsonRepresentation(BsonType.ObjectId)] public string BuilderId;
        public int BuildNumber;
        public ModpackBuildResult BuildResult = ModpackBuildResult.NONE;
        public GithubCommit Commit;
        public DateTime EndTime = DateTime.Now;
        public GameEnvironment Environment;
        public Dictionary<string, object> EnvironmentVariables = new();
        public bool Finished;
        public bool IsRebuild;
        public bool Running;
        public DateTime StartTime = DateTime.Now;
        public List<ModpackBuildStep> Steps = new();
        public string Version;
    }
}
