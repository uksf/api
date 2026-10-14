using System.Text.Json;
using static UKSF.Api.ArmaServer.Converters.PersistenceConversionHelpers;

namespace UKSF.Api.ArmaServer.Services;

public static class PersistenceSaveValidator
{
    private static readonly string[] ListFields = ["objects", "deletedObjects", "dateTime", "mapMarkers"];
    private static readonly int[] LoadoutListSlots = [0, 1, 2, 3, 4, 5, 8, 9];
    private static readonly int[] LoadoutStringSlots = [6, 7];

    public static void Validate(string key, Dictionary<string, object> raw)
    {
        foreach (var field in ListFields)
        {
            if (raw.TryGetValue(field, out var value) && value is not List<object> and not object[])
            {
                Reject(key, $"field '{field}' is not a list");
            }
        }

        if (!raw.TryGetValue("objects", out var objects))
        {
            Reject(key, "has no 'objects' field");
        }

        var ids = new HashSet<string>();
        foreach (var entry in ToList(objects))
        {
            if (ObjectProblem(entry) is { } problem)
            {
                Reject(key, $"field 'objects' holds an entry that is not a placed object: {problem}");
            }

            if (!ids.Add((string)ToDict(entry)["id"]))
            {
                Reject(key, $"field 'objects' holds the id '{ToDict(entry)["id"]}' more than once");
            }
        }

        if (!raw.TryGetValue("players", out var players))
        {
            return;
        }

        if (players is not Dictionary<string, object> and not List<object>)
        {
            Reject(key, "field 'players' is not a hashmap");
        }

        if (!IsHashmap(players))
        {
            Reject(key, "field 'players' holds an entry that is not a uid and player pair");
        }

        foreach (var (uid, entry) in ToDict(players))
        {
            if (PlayerProblem(uid, entry) is { } problem)
            {
                Reject(key, $"field 'players' holds an entry that is not a player: {problem}");
            }
        }
    }

    private static string ObjectProblem(object entry)
    {
        if (!IsHashmap(entry) || ToDict(entry) is not { Count: > 0 } fields)
        {
            return "not a hashmap";
        }

        if (!IsNonBlankString(fields.GetValueOrDefault("id"))) return "no id";
        if (!IsNonBlankString(fields.GetValueOrDefault("type"))) return "no type";
        if (!IsVector(fields.GetValueOrDefault("position"))) return "position is not three numbers";
        if (fields.GetValueOrDefault("vectorDirUp") is not List<object> { Count: 2 } dirUp || !dirUp.All(IsVector))
        {
            return "vectorDirUp is not two vectors";
        }

        return null;
    }

    private static string PlayerProblem(string uid, object entry)
    {
        if (string.IsNullOrWhiteSpace(uid)) return "blank uid";
        if (!IsHashmap(entry) || ToDict(entry) is not { Count: > 0 } fields) return $"'{uid}' is not a hashmap";
        if (!IsVector(fields.GetValueOrDefault("position"))) return $"'{uid}' position is not three numbers";
        if (!IsFiniteNumber(fields.GetValueOrDefault("direction"))) return $"'{uid}' direction is not a number";
        if (fields.GetValueOrDefault("damage") is not { } damage || !IsFiniteNumber(damage) || ToDouble(damage) is < 0 or > 1)
        {
            return $"'{uid}' damage is not between 0 and 1";
        }

        if (fields.GetValueOrDefault("animation") is not string) return $"'{uid}' animation is not a string";
        if (!IsVehicleState(fields.GetValueOrDefault("vehicleState"))) return $"'{uid}' vehicleState is malformed";
        if (!IsLoadout(fields.GetValueOrDefault("loadout"))) return $"'{uid}' loadout is malformed";
        if (!IsMedicalState(fields.GetValueOrDefault("aceMedical"))) return $"'{uid}' aceMedical is malformed";
        if (fields.GetValueOrDefault("earplugs") is not bool) return $"'{uid}' earplugs is not a boolean";
        if (fields.GetValueOrDefault("attachedItems") is not List<object> attached || !attached.All(item => item is string))
        {
            return $"'{uid}' attachedItems is not a list of strings";
        }

        if (fields.GetValueOrDefault("radios") is not List<object> radios || !radios.All(radio => radio is List<object>))
        {
            return $"'{uid}' radios is not a list of radios";
        }

        if (fields.GetValueOrDefault("diveState") is not List<object> { Count: > 0 } dive || dive[0] is not bool)
        {
            return $"'{uid}' diveState does not start with a boolean";
        }

        return null;
    }

    private static bool IsVehicleState(object value)
    {
        if (value is not List<object> { Count: 3 } state || state[0] is not string vehicleId || state[1] is not string role)
        {
            return false;
        }

        if (vehicleId.Length == 0)
        {
            return role.Length == 0 && IsWholeNumber(state[2]) && ToDouble(state[2]) == -1;
        }

        return role.Length > 0 && (IsWholeNumber(state[2]) || (state[2] is List<object> path && path.All(IsWholeNumber)));
    }

    private static bool IsLoadout(object value) =>
        value is List<object> { Count: 0 } ||
        value is List<object> { Count: 10 } loadout &&
        LoadoutListSlots.All(slot => loadout[slot] is List<object>) &&
        LoadoutStringSlots.All(slot => loadout[slot] is string);

    private static bool IsMedicalState(object value)
    {
        if (value is string json)
        {
            try
            {
                using var _ = JsonDocument.Parse(json);
                return true;
            }
            catch (JsonException)
            {
                return false;
            }
        }

        return IsHashmap(value);
    }

    private static bool IsVector(object value) => value is List<object> { Count: 3 } vector && vector.All(IsFiniteNumber);

    private static bool IsFiniteNumber(object value) =>
        value switch
        {
            long          => true,
            double number => double.IsFinite(number),
            _             => false
        };

    private static bool IsWholeNumber(object value) => IsFiniteNumber(value) && ToDouble(value) % 1 == 0;

    private static bool IsNonBlankString(object value) => value is string text && !string.IsNullOrWhiteSpace(text);

    internal static bool IsHashmap(object value) =>
        value switch
        {
            Dictionary<string, object> => true,
            List<object> list          => list.All(entry => entry is List<object> { Count: 2 } pair && pair[0] is string),
            _                          => false
        };

    private static void Reject(string key, string problem) => throw new PersistenceSaveRejectedException($"persistence_save for key '{key}' {problem}");
}
