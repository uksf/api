using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.Shared.Models;

namespace UKSF.Api.Modpack.Models;

public class ModpackBuild : MongoObject
{
    [BsonRepresentation(BsonType.ObjectId)]
    public string BuilderId { get; set; }

    public int BuildNumber { get; set; }
    public ModpackBuildResult BuildResult { get; set; } = ModpackBuildResult.NONE;
    public GithubCommit Commit { get; set; }
    public GameEnvironment Environment { get; set; }
    public Dictionary<string, object> EnvironmentVariables { get; set; } = new();
    public bool Finished { get; set; }
    public bool IsRebuild { get; set; }
    public bool Running { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public List<ModpackBuildStep> Steps { get; set; } = new();
    public string Version { get; set; }
}
