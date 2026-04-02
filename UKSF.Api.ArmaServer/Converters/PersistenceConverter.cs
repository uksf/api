using System.Text.RegularExpressions;
using UKSF.Api.ArmaServer.Models.Persistence;
using static UKSF.Api.ArmaServer.Converters.PersistenceConversionHelpers;

namespace UKSF.Api.ArmaServer.Converters;

public static partial class PersistenceConverter
{
    private const string KeyObjects = "uksf_persistence_objects";
    private const string KeyDeletedObjects = "uksf_persistence_deletedObjects";
    private const string KeyDateTime = "uksf_persistence_dateTime";
    private const string KeyMapMarkers = "uksf_persistence_mapMarkers";

    [GeneratedRegex(@"^\d{17}$")]
    private static partial Regex PlayerUidRegex();

    private static readonly HashSet<string> KnownKeys = [KeyObjects, KeyDeletedObjects, KeyDateTime, KeyMapMarkers];

    public static bool IsRawNamespaceFormat(Dictionary<string, object> raw)
    {
        return raw.ContainsKey(KeyObjects);
    }

    public static DomainPersistenceSession FromRawNamespace(Dictionary<string, object> raw)
    {
        var session = new DomainPersistenceSession
        {
            Objects = ToList(raw.GetValueOrDefault(KeyObjects)).Select(o => PersistenceObjectConverter.FromHashmap(ToDict(o))).ToList(),
            DeletedObjects = ToList(raw.GetValueOrDefault(KeyDeletedObjects)).Select(o => o.ToString()!).ToList(),
            ArmaDateTime = ToList(raw.GetValueOrDefault(KeyDateTime)).Select(ToInt).ToArray(),
            Markers = ToList(raw.GetValueOrDefault(KeyMapMarkers)).Select(o => ToList(o)).ToList()
        };

        var uidRegex = PlayerUidRegex();
        foreach (var kvp in raw)
        {
            if (KnownKeys.Contains(kvp.Key))
            {
                continue;
            }

            if (uidRegex.IsMatch(kvp.Key))
            {
                session.Players[kvp.Key] = PersistencePlayerConverter.FromArray(ToList(kvp.Value));
            }
            else
            {
                session.CustomData[kvp.Key] = kvp.Value;
            }
        }

        return session;
    }

    public static Dictionary<string, object> ToRawNamespace(DomainPersistenceSession session)
    {
        var raw = new Dictionary<string, object>
        {
            { KeyObjects, session.Objects.Select(o => (object)PersistenceObjectConverter.ToHashmap(o)).ToList() },
            { KeyDeletedObjects, session.DeletedObjects.Cast<object>().ToList() },
            { KeyDateTime, session.ArmaDateTime.Select(i => (object)(long)i).ToList() },
            { KeyMapMarkers, session.Markers.Select(m => (object)m).ToList() }
        };

        foreach (var kvp in session.Players)
        {
            raw[kvp.Key] = PersistencePlayerConverter.ToArray(kvp.Value);
        }

        foreach (var kvp in session.CustomData)
        {
            raw[kvp.Key] = kvp.Value;
        }

        return raw;
    }
}
