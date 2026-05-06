using System.Text.Json;
using UKSF.Api.ArmaServer.Models.Persistence;
using UKSF.Api.ArmaServer.Services;
using static UKSF.Api.ArmaServer.Converters.PersistenceConversionHelpers;

namespace UKSF.Api.ArmaServer.Converters;

public static class PersistencePlayerConverter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters =
        {
            new WoundEntryConverter(),
            new MedicationEntryConverter(),
            new OccludedMedicationEntryConverter(),
            new IvBagEntryConverter(),
            new TriageCardEntryConverter(),
            new MedicalLogCategoryConverter(),
            new MedicalLogEntryConverter()
        }
    };

    public static PlayerRedeployData FromHashmap(Dictionary<string, object> raw)
    {
        return new PlayerRedeployData
        {
            Position = ToList(raw.GetValueOrDefault("position")).Select(ToDouble).ToArray(),
            VehicleState = ParseVehicleState(ToList(raw.GetValueOrDefault("vehicleState"))),
            Direction = ToDouble(raw.GetValueOrDefault("direction") ?? 0.0),
            Animation = ToSafeString(raw.GetValueOrDefault("animation")),
            Loadout = PersistenceLoadoutConverter.FromArray(ToList(raw.GetValueOrDefault("loadout"))),
            Damage = ToDouble(raw.GetValueOrDefault("damage") ?? 0.0),
            AceMedical = ParseAceMedical(raw.GetValueOrDefault("aceMedical")),
            Earplugs = ToBool(raw.GetValueOrDefault("earplugs")),
            AttachedItems = ToList(raw.GetValueOrDefault("attachedItems")).Select(ToSafeString).ToArray(),
            Radios = ToList(raw.GetValueOrDefault("radios")).Select(ParseRadioState).ToList(),
            DiveState = ParseDiveState(ToList(raw.GetValueOrDefault("diveState")))
        };
    }

    public static Dictionary<string, object> ToHashmap(PlayerRedeployData player)
    {
        return new Dictionary<string, object>
        {
            ["position"] = player.Position.Cast<object>().ToList(),
            ["vehicleState"] = SerializeVehicleState(player.VehicleState),
            ["direction"] = player.Direction,
            ["animation"] = player.Animation,
            ["loadout"] = PersistenceLoadoutConverter.ToArray(player.Loadout),
            ["damage"] = player.Damage,
            ["aceMedical"] = SerializeAceMedical(player.AceMedical),
            ["earplugs"] = player.Earplugs,
            ["attachedItems"] = player.AttachedItems.Cast<object>().ToList(),
            ["radios"] = player.Radios.Select(SerializeRadioState).Cast<object>().ToList(),
            ["diveState"] = SerializeDiveState(player.DiveState)
        };
    }

    private static PlayerVehicleState ParseVehicleState(List<object> raw)
    {
        return new PlayerVehicleState
        {
            VehicleId = ToSafeString(raw[0]),
            Role = ToSafeString(raw[1]),
            Index = ParseVehicleIndex(raw[2])
        };
    }

    private static object ParseVehicleIndex(object raw)
    {
        if (raw is List<object> list)
        {
            return list.Select(ToInt).ToArray();
        }

        return ToInt(raw);
    }

    private static AceMedicalState ParseAceMedical(object raw)
    {
        // Legacy player records stored aceMedical as a JSON-encoded SQF string
        // (pre-HashMap ACE format). Parse it directly through the typed
        // deserialiser — same converters apply.
        if (raw is string legacyJson)
        {
            if (string.IsNullOrWhiteSpace(legacyJson)) return new AceMedicalState();
            try
            {
                return JsonSerializer.Deserialize<AceMedicalState>(legacyJson, JsonOptions) ?? new AceMedicalState();
            }
            catch (JsonException)
            {
                return new AceMedicalState();
            }
        }

        if (raw is not Dictionary<string, object> dict || dict.Count == 0)
        {
            return new AceMedicalState();
        }

        // SqfNotationParser.Normalize aggressively converts pair-of-string-keyed lists
        // into Dictionary<string,object>. That misfires for a few ACE fields whose
        // canonical SQF shape is "array of [string, list] pairs" (a list, not a hashmap)
        // or "empty list standing in for empty hashmap". Massage the shape here so the
        // typed deserialiser sees the JSON the converters expect.
        var normalised = new Dictionary<string, object>(dict, StringComparer.OrdinalIgnoreCase);

        // ace_medical_logs: SQF emits [[logType, [entries]], ...]. Parser flips it to
        // a Dict on misdetection. Reverse so MedicalLogCategoryConverter sees an array.
        if (normalised.TryGetValue("ace_medical_logs", out var logsRaw) && logsRaw is Dictionary<string, object> logsDict)
        {
            normalised["ace_medical_logs"] = logsDict.Select(kvp => (object)new List<object> { kvp.Key, kvp.Value }).ToList();
        }

        // Wound buckets: empty list stands in for empty hashmap when no wounds exist.
        foreach (var key in new[] { "ace_medical_openwounds", "ace_medical_bandagedwounds", "ace_medical_stitchedwounds" })
        {
            if (normalised.TryGetValue(key, out var bucket) && bucket is List<object> { Count: 0 })
            {
                normalised[key] = new Dictionary<string, object>();
            }
        }

        var json = JsonSerializer.Serialize(normalised);
        return JsonSerializer.Deserialize<AceMedicalState>(json, JsonOptions) ?? new AceMedicalState();
    }

    private static RadioState ParseRadioState(object raw)
    {
        var list = ToList(raw);
        if (list.Count < 5)
        {
            return new RadioState();
        }

        return new RadioState
        {
            Type = ToSafeString(list[0]),
            Channel = ToInt(list[1]),
            Volume = ToDouble(list[2]),
            Spatial = ToSafeString(list[3]),
            PttIndex = ToInt(list[4])
        };
    }

    private static PlayerDiveState ParseDiveState(List<object> raw)
    {
        if (raw.Count == 0)
        {
            return new PlayerDiveState();
        }

        var isDiving = ToBool(raw[0]);
        return new PlayerDiveState { IsDiving = isDiving, RawData = isDiving ? raw.Skip(1).ToList() : [] };
    }

    private static List<object> SerializeVehicleState(PlayerVehicleState state)
    {
        object index = state.Index switch
        {
            int[] arr => arr.Cast<object>().ToList(),
            int i     => (long)i,
            long l    => l,
            _         => state.Index
        };

        return [state.VehicleId, state.Role, index];
    }

    private static List<object> SerializeRadioState(RadioState radio)
    {
        return [(object)radio.Type, (long)radio.Channel, radio.Volume, radio.Spatial, (long)radio.PttIndex];
    }

    private static Dictionary<string, object> SerializeAceMedical(AceMedicalState state)
    {
        // Round-trip through JSON so nested ACE entries emit as positional arrays (via the
        // registered converters), matching the wire format from ace_medical_fnc_serializeState.
        var json = JsonSerializer.Serialize(state, JsonOptions);
        return JsonSerializer.Deserialize<Dictionary<string, object>>(json, PersistenceSessionsService.SerializerOptions) ?? new Dictionary<string, object>();
    }

    private static List<object> SerializeDiveState(PlayerDiveState state)
    {
        var result = new List<object> { state.IsDiving };
        if (state.IsDiving)
        {
            result.AddRange(state.RawData);
        }

        return result;
    }
}
