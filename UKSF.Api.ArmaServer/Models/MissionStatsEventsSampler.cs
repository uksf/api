using UKSF.Api.Core.Models;

namespace UKSF.Api.ArmaServer.Models;

public class MissionStatsEventsSampler : MongoObject
{
    public string MissionSessionId { get; set; } = string.Empty;
    public string PlayerUid { get; set; } = string.Empty;
    public DateTime FirstTimestamp { get; set; }
    public DateTime LastTimestamp { get; set; }

    public List<double> DistanceOnFoot { get; set; } = [];
    public List<double> DistanceInVehicle { get; set; } = [];
    public List<double> FuelLitres { get; set; } = [];
}
