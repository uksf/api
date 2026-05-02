using UKSF.Api.ArmaServer.Models.Persistence;
using static UKSF.Api.ArmaServer.Converters.PersistenceConversionHelpers;

namespace UKSF.Api.ArmaServer.Converters;

public static class PersistenceConverter
{
    private static readonly HashSet<string> KnownKeys = ["objects", "deletedObjects", "dateTime", "mapMarkers", "players"];

    public static DomainPersistenceSession FromHashmap(Dictionary<string, object> raw)
    {
        var session = new DomainPersistenceSession
        {
            Objects = ToList(raw.GetValueOrDefault("objects")).Select(o => PersistenceObjectConverter.FromHashmap(ToDict(o))).ToList(),
            DeletedObjects = ToList(raw.GetValueOrDefault("deletedObjects")).Select(o => o.ToString()!).ToList(),
            ArmaDateTime = ToList(raw.GetValueOrDefault("dateTime")).Select(ToInt).ToArray(),
            Markers = ToList(raw.GetValueOrDefault("mapMarkers")).Select(o => ToList(o)).ToList()
        };

        // Players are nested under a "players" key as a dictionary of UID → data.
        // Source can be either a Dictionary (from JSON) or a pair-list (from SQF str output).
        if (raw.TryGetValue("players", out var playersObj))
        {
            foreach (var kvp in ToDict(playersObj))
            {
                session.Players[kvp.Key] = PersistencePlayerConverter.FromHashmap(ToDict(kvp.Value));
            }
        }

        // Everything else is custom serialiser data
        foreach (var kvp in raw)
        {
            if (!KnownKeys.Contains(kvp.Key))
            {
                session.CustomData[kvp.Key] = kvp.Value;
            }
        }

        return session;
    }

    public static Dictionary<string, object> ToHashmap(DomainPersistenceSession session)
    {
        var raw = new Dictionary<string, object>
        {
            { "objects", session.Objects.Select(o => (object)PersistenceObjectConverter.ToHashmap(o)).ToList() },
            { "deletedObjects", session.DeletedObjects.Cast<object>().ToList() },
            { "dateTime", session.ArmaDateTime.Select(i => (object)(long)i).ToList() },
            { "mapMarkers", session.Markers.Select(m => (object)m).ToList() },
            { "players", session.Players.ToDictionary(kvp => kvp.Key, kvp => (object)PersistencePlayerConverter.ToHashmap(kvp.Value)) }
        };

        foreach (var kvp in session.CustomData)
        {
            raw[kvp.Key] = kvp.Value;
        }

        return raw;
    }
}
