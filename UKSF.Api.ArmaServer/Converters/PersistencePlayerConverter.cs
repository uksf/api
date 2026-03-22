using System.Text.Json;
using UKSF.Api.ArmaServer.Models.Persistence;
using static UKSF.Api.ArmaServer.Converters.PersistenceConversionHelpers;

namespace UKSF.Api.ArmaServer.Converters;

public static class PersistencePlayerConverter
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public static PlayerRedeployData FromArray(List<object> raw)
    {
        return new PlayerRedeployData
        {
            Position = ToList(raw[0]).Select(ToDouble).ToArray(),
            VehicleState = ParseVehicleState(ToList(raw[1])),
            Direction = ToDouble(raw[2]),
            Animation = ToSafeString(raw[3]),
            Loadout = PersistenceLoadoutConverter.FromArray(ToList(raw[4])),
            Damage = ToDouble(raw[5]),
            AceMedical = ParseAceMedical(raw[6]),
            Earplugs = ToBool(raw[7]),
            AttachedItems = ToList(raw[8]).Select(ToSafeString).ToArray(),
            Radios = ToList(raw[9]).Select(ParseRadioState).ToList(),
            DiveState = ParseDiveState(ToList(raw[10]))
        };
    }

    public static List<object> ToArray(PlayerRedeployData player)
    {
        return
        [
            player.Position.Cast<object>().ToList(),
            SerializeVehicleState(player.VehicleState),
            player.Direction,
            player.Animation,
            PersistenceLoadoutConverter.ToArray(player.Loadout),
            player.Damage,
            JsonSerializer.Serialize(player.AceMedical, JsonOptions),
            player.Earplugs,
            player.AttachedItems.Cast<object>().ToList(),
            player.Radios.Select(SerializeRadioState).Cast<object>().ToList(),
            SerializeDiveState(player.DiveState)
        ];
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
        var json = ToSafeString(raw);
        if (string.IsNullOrEmpty(json) || json == "{}")
        {
            return new AceMedicalState();
        }

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
