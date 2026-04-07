using MongoDB.Bson;

namespace UKSF.Api.ArmaServer.Services.StatsEventProcessors;

/// <summary>
/// Helpers for gap-compacted sampled series. A series stores positive values
/// as tick measurements; negative values are run-length counts of idle ticks.
/// </summary>
public static class SampledSeries
{
    public static double SumPositive(BsonValue value)
    {
        if (value is not BsonArray array)
        {
            return 0;
        }

        double total = 0;
        foreach (var entry in array)
        {
            if (!entry.IsNumeric)
            {
                continue;
            }

            var numeric = entry.ToDouble();
            if (numeric > 0)
            {
                total += numeric;
            }
        }

        return total;
    }
}
