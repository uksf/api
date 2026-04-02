using UKSF.Api.ArmaServer.Models.Persistence;
using static UKSF.Api.ArmaServer.Converters.PersistenceConversionHelpers;

namespace UKSF.Api.ArmaServer.Converters;

public static class PersistenceObjectConverter
{
    public static PersistenceObject FromHashmap(Dictionary<string, object> raw)
    {
        return new PersistenceObject
        {
            Id = ToSafeString(raw.GetValueOrDefault("id")),
            Type = ToSafeString(raw.GetValueOrDefault("type")),
            Position = ToList(raw.GetValueOrDefault("position")).Select(ToDouble).ToArray(),
            VectorDirUp = ToList(raw.GetValueOrDefault("vectorDirUp")).Select(v => ToList(v).Select(ToDouble).ToArray()).ToArray(),
            Damage = ToDouble(raw.GetValueOrDefault("damage") ?? 0.0),
            Fuel = ToDouble(raw.GetValueOrDefault("fuel") ?? 0.0),
            TurretWeapons = ParseTurretWeapons(ToList(raw.GetValueOrDefault("turretWeapons"))),
            TurretMagazines = ParseTurretMagazines(ToList(raw.GetValueOrDefault("turretMagazines"))),
            PylonLoadout = ParsePylonLoadout(ToList(raw.GetValueOrDefault("pylonLoadout"))),
            Logistics = ToList(raw.GetValueOrDefault("logistics")).Select(ToDouble).ToArray(),
            Attached = ParseAttached(ToList(raw.GetValueOrDefault("attached"))),
            RackChannels = ToList(raw.GetValueOrDefault("rackChannels")).Select(ToInt).ToArray(),
            AceCargo = ParseAceCargo(ToList(raw.GetValueOrDefault("aceCargo"))),
            Inventory = ParseInventory(ToList(raw.GetValueOrDefault("inventory"))),
            AceFortify = ParseAceFortify(ToList(raw.GetValueOrDefault("aceFortify"))),
            AceMedical = ParseAceMedical(ToList(raw.GetValueOrDefault("aceMedical"))),
            AceRepair = ParseAceRepair(ToList(raw.GetValueOrDefault("aceRepair"))),
            CustomName = ToSafeString(raw.GetValueOrDefault("customName"))
        };
    }

    public static Dictionary<string, object> ToHashmap(PersistenceObject obj) =>
        new()
        {
            ["id"] = obj.Id,
            ["type"] = obj.Type,
            ["position"] = obj.Position.Cast<object>().ToList(),
            ["vectorDirUp"] = obj.VectorDirUp.Select(v => (object)v.Cast<object>().ToList()).ToList(),
            ["damage"] = obj.Damage,
            ["fuel"] = obj.Fuel,
            ["turretWeapons"] = SerializeTurretWeapons(obj.TurretWeapons),
            ["turretMagazines"] = SerializeTurretMagazines(obj.TurretMagazines),
            ["pylonLoadout"] = SerializePylonLoadout(obj.PylonLoadout),
            ["logistics"] = obj.Logistics.Cast<object>().ToList(),
            ["attached"] = SerializeAttached(obj.Attached),
            ["rackChannels"] = obj.RackChannels.Cast<object>().ToList(),
            ["aceCargo"] = SerializeAceCargo(obj.AceCargo),
            ["inventory"] = SerializeInventory(obj.Inventory),
            ["aceFortify"] = new List<object> { obj.AceFortify.IsAceFortification, obj.AceFortify.Side },
            ["aceMedical"] = new List<object>
            {
                obj.AceMedical.MedicClass,
                obj.AceMedical.MedicalVehicle,
                obj.AceMedical.MedicalFacility
            },
            ["aceRepair"] = new List<object> { (object)obj.AceRepair.RepairVehicle, (object)obj.AceRepair.RepairFacility },
            ["customName"] = obj.CustomName
        };

    private static List<TurretWeaponsEntry> ParseTurretWeapons(List<object> raw) =>
        raw.Select(entry =>
               {
                   var list = ToList(entry);
                   return new TurretWeaponsEntry
                       {
                           TurretPath = ToList(list[0]).Select(ToInt).ToArray(), Weapons = ToList(list[1]).Select(ToSafeString).ToArray()
                       };
               }
           )
           .ToList();

    private static List<TurretMagazineEntry> ParseTurretMagazines(List<object> raw) =>
        raw.Select(entry =>
               {
                   var list = ToList(entry);
                   if (list.Count < 3)
                   {
                       return new TurretMagazineEntry();
                   }

                   return new TurretMagazineEntry
                   {
                       ClassName = ToSafeString(list[0]),
                       TurretPath = ToList(list[1]).Select(ToInt).ToArray(),
                       AmmoCount = ToInt(list[2])
                   };
               }
           )
           .ToList();

    private static List<PylonEntry> ParsePylonLoadout(List<object> raw) =>
        raw.Select(entry =>
               {
                   var list = ToList(entry);
                   return new PylonEntry { Magazine = ToSafeString(list[0]), Ammo = ToInt(list[1]) };
               }
           )
           .ToList();

    private static List<AttachedObject> ParseAttached(List<object> raw) =>
        raw.Select(entry =>
               {
                   var list = ToList(entry);
                   return new AttachedObject { ClassName = ToSafeString(list[0]), Offset = ToList(list[1]).Select(ToDouble).ToArray() };
               }
           )
           .ToList();

    private static List<AceCargoEntry> ParseAceCargo(List<object> raw) =>
        raw.Select(entry =>
               {
                   var list = ToList(entry);
                   return new AceCargoEntry
                   {
                       ClassName = ToSafeString(list[0]),
                       Cargo = ParseAceCargo(ToList(list[1])),
                       Inventory = ParseInventory(ToList(list[2])),
                       CustomName = ToSafeString(list[3])
                   };
               }
           )
           .ToList();

    private static InventoryContainer ParseInventory(List<object> raw)
    {
        if (raw.Count < 4)
        {
            return new InventoryContainer();
        }

        return new InventoryContainer
        {
            Weapons = ParseCargoSlot(ToList(raw[0])),
            Magazines = ParseCargoSlot(ToList(raw[1])),
            Items = ParseCargoSlot(ToList(raw[2])),
            Backpacks = ParseCargoSlot(ToList(raw[3]))
        };
    }

    private static CargoSlot ParseCargoSlot(List<object> raw)
    {
        if (raw.Count < 2)
        {
            return new CargoSlot();
        }

        return new CargoSlot { ClassNames = ToList(raw[0]).Select(ToSafeString).ToArray(), Counts = ToList(raw[1]).Select(ToInt).ToArray() };
    }

    private static AceFortifyState ParseAceFortify(List<object> raw)
    {
        if (raw.Count < 2)
        {
            return new AceFortifyState();
        }

        return new AceFortifyState { IsAceFortification = ToBool(raw[0]), Side = ToSafeString(raw[1]) };
    }

    private static ObjectMedicalState ParseAceMedical(List<object> raw)
    {
        if (raw.Count < 3)
        {
            return new ObjectMedicalState();
        }

        return new ObjectMedicalState
        {
            MedicClass = ToInt(raw[0]),
            MedicalVehicle = ToBool(raw[1]),
            MedicalFacility = ToBool(raw[2])
        };
    }

    private static ObjectRepairState ParseAceRepair(List<object> raw)
    {
        if (raw.Count < 2)
        {
            return new ObjectRepairState();
        }

        return new ObjectRepairState { RepairVehicle = ToInt(raw[0]), RepairFacility = ToInt(raw[1]) };
    }

    private static List<object> SerializeTurretWeapons(List<TurretWeaponsEntry> entries) =>
        entries.Select(e => (object)new List<object> { e.TurretPath.Cast<object>().ToList(), e.Weapons.Cast<object>().ToList() }).ToList();

    private static List<object> SerializeTurretMagazines(List<TurretMagazineEntry> entries) =>
        entries.Select(e => (object)new List<object>
                   {
                       e.ClassName,
                       e.TurretPath.Cast<object>().ToList(),
                       e.AmmoCount
                   }
               )
               .ToList();

    private static List<object> SerializePylonLoadout(List<PylonEntry> entries) =>
        entries.Select(e => (object)new List<object> { e.Magazine, e.Ammo }).ToList();

    private static List<object> SerializeAttached(List<AttachedObject> entries) =>
        entries.Select(e => (object)new List<object> { e.ClassName, e.Offset.Cast<object>().ToList() }).ToList();

    private static List<object> SerializeAceCargo(List<AceCargoEntry> entries) =>
        entries.Select(e => (object)new List<object>
                   {
                       e.ClassName,
                       SerializeAceCargo(e.Cargo),
                       SerializeInventory(e.Inventory),
                       e.CustomName
                   }
               )
               .ToList();

    private static object SerializeInventory(InventoryContainer inv)
    {
        // If all cargo slots are empty, return an empty list to match the SQF representation.
        // SQF sends [] for empty inventories; inflating to [[[],[]],[[],[]],[[],[]],[[],[]]] would mismatch.
        if (inv.Weapons.ClassNames.Length == 0 &&
            inv.Magazines.ClassNames.Length == 0 &&
            inv.Items.ClassNames.Length == 0 &&
            inv.Backpacks.ClassNames.Length == 0)
        {
            return new List<object>();
        }

        return new List<object>
        {
            SerializeCargoSlot(inv.Weapons),
            SerializeCargoSlot(inv.Magazines),
            SerializeCargoSlot(inv.Items),
            SerializeCargoSlot(inv.Backpacks)
        };
    }

    private static List<object> SerializeCargoSlot(CargoSlot slot) => new() { slot.ClassNames.Cast<object>().ToList(), slot.Counts.Cast<object>().ToList() };
}
