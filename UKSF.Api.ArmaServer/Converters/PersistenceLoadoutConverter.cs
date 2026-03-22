using UKSF.Api.ArmaServer.Models.Persistence;

namespace UKSF.Api.ArmaServer.Converters;

public static class PersistenceLoadoutConverter
{
    public static ArmaLoadout FromArray(List<object> raw)
    {
        if (raw.Count < 10)
        {
            return new ArmaLoadout();
        }

        return new ArmaLoadout
        {
            PrimaryWeapon = ParseWeaponSlot(ToList(raw[0])),
            SecondaryWeapon = ParseWeaponSlot(ToList(raw[1])),
            Handgun = ParseWeaponSlot(ToList(raw[2])),
            Uniform = ParseContainerSlot(ToList(raw[3])),
            Vest = ParseContainerSlot(ToList(raw[4])),
            Backpack = ParseContainerSlot(ToList(raw[5])),
            Headgear = ToString(raw[6]),
            Facewear = ToString(raw[7]),
            Binoculars = ParseWeaponSlot(ToList(raw[8])),
            LinkedItems = ParseLinkedItems(ToList(raw[9]))
        };
    }

    public static List<object> ToArray(ArmaLoadout loadout)
    {
        return
        [
            SerializeWeaponSlot(loadout.PrimaryWeapon),
            SerializeWeaponSlot(loadout.SecondaryWeapon),
            SerializeWeaponSlot(loadout.Handgun),
            SerializeContainerSlot(loadout.Uniform),
            SerializeContainerSlot(loadout.Vest),
            SerializeContainerSlot(loadout.Backpack),
            loadout.Headgear,
            loadout.Facewear,
            SerializeWeaponSlot(loadout.Binoculars),
            new List<object>
            {
                loadout.LinkedItems.Map,
                loadout.LinkedItems.Gps,
                loadout.LinkedItems.Radio,
                loadout.LinkedItems.Compass,
                loadout.LinkedItems.Watch,
                loadout.LinkedItems.Nvg
            }
        ];
    }

    private static WeaponSlot ParseWeaponSlot(List<object> raw)
    {
        if (raw.Count < 7)
        {
            return new WeaponSlot();
        }

        return new WeaponSlot
        {
            Weapon = ToString(raw[0]),
            Muzzle = ToString(raw[1]),
            Pointer = ToString(raw[2]),
            Optic = ToString(raw[3]),
            PrimaryMagazine = ParseMagazineState(ToList(raw[4])),
            SecondaryMagazine = ParseMagazineState(ToList(raw[5])),
            Bipod = ToString(raw[6])
        };
    }

    private static MagazineState ParseMagazineState(List<object> raw)
    {
        if (raw.Count < 2)
        {
            return new MagazineState();
        }

        return new MagazineState { ClassName = ToString(raw[0]), Ammo = ToInt(raw[1]) };
    }

    private static ContainerSlot ParseContainerSlot(List<object> raw)
    {
        if (raw.Count < 2)
        {
            return new ContainerSlot();
        }

        return new ContainerSlot { ClassName = ToString(raw[0]), Items = ToList(raw[1]).Select(ParseContainerItem).ToList() };
    }

    private static ContainerItem ParseContainerItem(object raw)
    {
        var list = ToList(raw);

        if (list.Count >= 1 && list[0] is List<object>)
        {
            return new ContainerItem
            {
                Type = "weapon",
                Weapon = ParseWeaponSlot(ToList(list[0])),
                Count = list.Count >= 2 ? ToInt(list[1]) : 1
            };
        }

        if (list.Count == 2 && list[1] is bool)
        {
            return new ContainerItem
            {
                Type = "container",
                ClassName = ToString(list[0]),
                IsBackpack = ToBool(list[1])
            };
        }

        if (list.Count == 3)
        {
            return new ContainerItem
            {
                Type = "magazine",
                ClassName = ToString(list[0]),
                Count = ToInt(list[1]),
                Ammo = ToInt(list[2])
            };
        }

        return new ContainerItem
        {
            Type = "item",
            ClassName = ToString(list[0]),
            Count = list.Count >= 2 ? ToInt(list[1]) : 0
        };
    }

    private static LinkedItems ParseLinkedItems(List<object> raw)
    {
        if (raw.Count < 6)
        {
            return new LinkedItems();
        }

        return new LinkedItems
        {
            Map = ToString(raw[0]),
            Gps = ToString(raw[1]),
            Radio = ToString(raw[2]),
            Compass = ToString(raw[3]),
            Watch = ToString(raw[4]),
            Nvg = ToString(raw[5])
        };
    }

    private static List<object> SerializeWeaponSlot(WeaponSlot slot)
    {
        if (string.IsNullOrEmpty(slot.Weapon))
        {
            return [];
        }

        return
        [
            slot.Weapon,
            slot.Muzzle,
            slot.Pointer,
            slot.Optic,
            SerializeMagazineState(slot.PrimaryMagazine),
            SerializeMagazineState(slot.SecondaryMagazine),
            slot.Bipod
        ];
    }

    private static List<object> SerializeMagazineState(MagazineState mag)
    {
        if (string.IsNullOrEmpty(mag.ClassName))
        {
            return [];
        }

        return [mag.ClassName, (long)mag.Ammo];
    }

    private static List<object> SerializeContainerSlot(ContainerSlot slot)
    {
        if (string.IsNullOrEmpty(slot.ClassName))
        {
            return [];
        }

        return [slot.ClassName, slot.Items.Select(SerializeContainerItem).Cast<object>().ToList()];
    }

    private static List<object> SerializeContainerItem(ContainerItem item)
    {
        return item.Type switch
        {
            "weapon"    => [SerializeWeaponSlot(item.Weapon!), (long)item.Count],
            "container" => [item.ClassName, item.IsBackpack!.Value],
            "magazine"  => [item.ClassName, (long)item.Count, (long)item.Ammo!.Value],
            _           => [item.ClassName, (long)item.Count]
        };
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
}
