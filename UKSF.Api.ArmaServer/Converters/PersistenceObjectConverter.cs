using UKSF.Api.ArmaServer.Models.Persistence;

namespace UKSF.Api.ArmaServer.Converters;

public static class PersistenceObjectConverter
{
    public static PersistenceObject FromArray(List<object> raw)
    {
        var obj = new PersistenceObject
        {
            Id = ToString(raw[0]),
            Type = ToString(raw[1]),
            Position = ToList(raw[2]).Select(ToDouble).ToArray(),
            VectorDirUp = ToList(raw[3]).Select(v => ToList(v).Select(ToDouble).ToArray()).ToArray(),
            Damage = ToDouble(raw[4]),
            Fuel = ToDouble(raw[5]),
            TurretWeapons = ParseTurretWeapons(ToList(raw[6])),
            TurretMagazines = ParseTurretMagazines(ToList(raw[7])),
            PylonLoadout = ParsePylonLoadout(ToList(raw[8])),
            Logistics = ToList(raw[9]).Select(ToDouble).ToArray(),
            Attached = ParseAttached(ToList(raw[10])),
            RackChannels = ToList(raw[11]).Select(ToInt).ToArray(),
            AceCargo = ParseAceCargo(ToList(raw[12])),
            Inventory = ParseInventory(ToList(raw[13])),
            AceFortify = ParseAceFortify(ToList(raw[14])),
            AceMedical = ParseAceMedical(ToList(raw[15])),
            AceRepair = ParseAceRepair(ToList(raw[16])),
            CustomName = ToString(raw[17])
        };

        return obj;
    }

    public static List<object> ToArray(PersistenceObject obj)
    {
        return
        [
            obj.Id,
            obj.Type,
            obj.Position.Cast<object>().ToList(),
            obj.VectorDirUp.Select(v => (object)v.Cast<object>().ToList()).ToList(),
            obj.Damage,
            obj.Fuel,
            SerializeTurretWeapons(obj.TurretWeapons),
            SerializeTurretMagazines(obj.TurretMagazines),
            SerializePylonLoadout(obj.PylonLoadout),
            obj.Logistics.Cast<object>().ToList(),
            SerializeAttached(obj.Attached),
            obj.RackChannels.Cast<object>().ToList(),
            SerializeAceCargo(obj.AceCargo),
            SerializeInventory(obj.Inventory),
            new List<object> { obj.AceFortify.IsAceFortification, obj.AceFortify.Side },
            new List<object>
            {
                obj.AceMedical.MedicClass,
                obj.AceMedical.MedicalVehicle,
                obj.AceMedical.MedicalFacility
            },
            new List<object> { (object)obj.AceRepair.RepairVehicle, (object)obj.AceRepair.RepairFacility },
            obj.CustomName
        ];
    }

    private static double ToDouble(object v) =>
        v switch
        {
            double d => d,
            long l   => l,
            int i    => i,
            float f  => f,
            _        => Convert.ToDouble(v)
        };

    private static int ToInt(object v) =>
        v switch
        {
            int i    => i,
            long l   => (int)l,
            double d => (int)d,
            _        => Convert.ToInt32(v)
        };

    private static string ToString(object v) => v?.ToString() ?? string.Empty;

    private static bool ToBool(object v) =>
        v switch
        {
            bool b => b,
            _      => Convert.ToBoolean(v)
        };

    private static List<object> ToList(object v) =>
        v switch
        {
            List<object> list => list,
            object[] array    => [..array],
            _                 => []
        };

    private static List<TurretWeaponsEntry> ParseTurretWeapons(List<object> raw) =>
        raw.Select(entry =>
               {
                   var list = ToList(entry);
                   return new TurretWeaponsEntry { TurretPath = ToList(list[0]).Select(ToInt).ToArray(), Weapons = ToList(list[1]).Select(ToString).ToArray() };
               }
           )
           .ToList();

    private static List<TurretMagazineEntry> ParseTurretMagazines(List<object> raw) =>
        raw.Select(entry =>
               {
                   var list = ToList(entry);
                   return new TurretMagazineEntry
                   {
                       ClassName = ToString(list[0]),
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
                   return new PylonEntry { Magazine = ToString(list[0]), Ammo = ToInt(list[1]) };
               }
           )
           .ToList();

    private static List<AttachedObject> ParseAttached(List<object> raw) =>
        raw.Select(entry =>
               {
                   var list = ToList(entry);
                   return new AttachedObject { ClassName = ToString(list[0]), Offset = ToList(list[1]).Select(ToDouble).ToArray() };
               }
           )
           .ToList();

    private static List<AceCargoEntry> ParseAceCargo(List<object> raw) =>
        raw.Select(entry =>
               {
                   var list = ToList(entry);
                   return new AceCargoEntry
                   {
                       ClassName = ToString(list[0]),
                       Cargo = ParseAceCargo(ToList(list[1])),
                       Inventory = ParseInventory(ToList(list[2])),
                       CustomName = ToString(list[3])
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

        return new CargoSlot { ClassNames = ToList(raw[0]).Select(ToString).ToArray(), Counts = ToList(raw[1]).Select(ToInt).ToArray() };
    }

    private static AceFortifyState ParseAceFortify(List<object> raw) => new() { IsAceFortification = ToBool(raw[0]), Side = ToString(raw[1]) };

    private static ObjectMedicalState ParseAceMedical(List<object> raw) =>
        new()
        {
            MedicClass = ToInt(raw[0]),
            MedicalVehicle = ToBool(raw[1]),
            MedicalFacility = ToBool(raw[2])
        };

    private static ObjectRepairState ParseAceRepair(List<object> raw) => new() { RepairVehicle = ToInt(raw[0]), RepairFacility = ToInt(raw[1]) };

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

    private static List<object> SerializeInventory(InventoryContainer inv) =>
    [
        SerializeCargoSlot(inv.Weapons),
        SerializeCargoSlot(inv.Magazines),
        SerializeCargoSlot(inv.Items),
        SerializeCargoSlot(inv.Backpacks)
    ];

    private static List<object> SerializeCargoSlot(CargoSlot slot) => new() { slot.ClassNames.Cast<object>().ToList(), slot.Counts.Cast<object>().ToList() };
}
