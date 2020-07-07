using System;
using System.Collections.Generic;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using UKSF.Api.Models.Integrations.Github;

namespace UKSF.Api.Models.Modpack {
    public class ModpackBuild : DatabaseObject {
        [BsonRepresentation(BsonType.ObjectId)] public string builderId;
        public int buildNumber;
        public ModpackBuildResult buildResult = ModpackBuildResult.NONE;
        public GithubCommit commit;
        public bool finished;
        public bool isRelease;
        public bool isReleaseCandidate;
        public bool running;
        public List<ModpackBuildStep> steps = new List<ModpackBuildStep>();
        public DateTime startTime = DateTime.Now;
        public DateTime endTime = DateTime.Now;
        public string version;
    }
}
