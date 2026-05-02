namespace UKSF.Api.ArmaServer.Converters;

internal static class PersistenceConversionHelpers
{
    internal static double ToDouble(object v) =>
        v switch
        {
            double d => d,
            long l   => l,
            int i    => i,
            float f  => f,
            _        => Convert.ToDouble(v)
        };

    internal static int ToInt(object v) =>
        v switch
        {
            int i    => i,
            long l   => (int)l,
            double d => (int)d,
            _        => Convert.ToInt32(v)
        };

    internal static string ToSafeString(object v) => v?.ToString() ?? string.Empty;

    internal static bool ToBool(object v) =>
        v switch
        {
            bool b => b,
            _      => Convert.ToBoolean(v)
        };

    internal static List<object> ToList(object v) =>
        v switch
        {
            List<object> list => list,
            object[] array    => [..array],
            _                 => []
        };

    internal static Dictionary<string, object> ToDict(object v) =>
        v switch
        {
            Dictionary<string, object> dict => dict,
            List<object> list               => PairListToDict(list),
            _                               => new Dictionary<string, object>()
        };

    private static Dictionary<string, object> PairListToDict(List<object> list)
    {
        var dict = new Dictionary<string, object>(list.Count);
        foreach (var entry in list)
        {
            if (entry is List<object> pair && pair.Count == 2 && pair[0] is string key)
            {
                dict[key] = pair[1];
            }
        }

        return dict;
    }
}
