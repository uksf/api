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
        if (raw is not Dictionary<string, object> dict || dict.Count == 0)
        {
            return new AceMedicalState();
        }

        var json = JsonSerializer.Serialize(dict);
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
