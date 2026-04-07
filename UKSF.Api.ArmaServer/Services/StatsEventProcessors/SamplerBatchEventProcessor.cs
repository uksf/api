using MongoDB.Bson;
using UKSF.Api.ArmaServer.Models;

namespace UKSF.Api.ArmaServer.Services.StatsEventProcessors;

/// <summary>
/// Processes a batched sampler event containing gap-compacted per-tick series
/// for distance on foot, distance in vehicle, and fuel consumed in litres.
/// Sums positive entries and increments the corresponding scalar totals on
/// PlayerMissionStats.
/// </summary>
public class SamplerBatchEventProcessor : IStatsEventProcessor
{
    public string EventType => "samplerBatch";

    public void ProcessForPlayer(BsonDocument evt, PlayerMissionStats stats)
    {
        stats.DistanceOnFoot += SampledSeries.SumPositive(evt.GetValue("distanceOnFoot", BsonNull.Value));
        stats.DistanceInVehicle += SampledSeries.SumPositive(evt.GetValue("distanceInVehicle", BsonNull.Value));
        stats.TotalFuelLitres += SampledSeries.SumPositive(evt.GetValue("fuelLitres", BsonNull.Value));
    }
}
