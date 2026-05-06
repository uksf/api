using UKSF.Api.ArmaServer.Models.Persistence;
using static UKSF.Api.ArmaServer.Converters.PersistenceConversionHelpers;

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
            Headgear = ToSafeString(raw[6]),
            Facewear = ToSafeString(raw[7]),
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
            Weapon = ToSafeString(raw[0]),
            Muzzle = ToSafeString(raw[1]),
            Pointer = ToSafeString(raw[2]),
            Optic = ToSafeString(raw[3]),
            PrimaryMagazine = ParseMagazineState(ToList(raw[4])),
            SecondaryMagazine = ParseMagazineState(ToList(raw[5])),
            Bipod = ToSafeString(raw[6])
        };
    }

    private static MagazineState ParseMagazineState(List<object> raw)
    {
        if (raw.Count < 2)
        {
            return new MagazineState();
        }

        return new MagazineState { ClassName = ToSafeString(raw[0]), Ammo = ToInt(raw[1]) };
    }

    private static ContainerSlot ParseContainerSlot(List<object> raw)
    {
        if (raw.Count < 2)
        {
            return new ContainerSlot();
        }

        // raw[1] is the items list — uniformly 2-elem [name,count] entries fool the
        // SqfNotationParser hashmap heuristic into producing a Dict; force back to list.
        return new ContainerSlot { ClassName = ToSafeString(raw[0]), Items = ToListFromAny(raw[1]).Select(ParseContainerItem).ToList() };
    }

    private static ContainerItem ParseContainerItem(object raw)
    {
        var list = ToList(raw);

        // Weapon stowed in container: [[weapon, muzzle, pointer, optic, [primMag,count], [secMag,count], bipod], count]
        if (list.Count >= 1 && list[0] is List<object> or object[])
        {
            return new ContainerItem
            {
                Type = "weapon",
                Weapon = ParseWeaponSlot(ToList(list[0])),
                Count = list.Count >= 2 ? ToInt(list[1]) : 1
            };
        }

        // Sub-container (backpack inside backpack): [className, isBackpack]
        if (list.Count == 2 && list[1] is bool)
        {
            return new ContainerItem
            {
                Type = "container",
                ClassName = ToSafeString(list[0]),
                IsBackpack = ToBool(list[1])
            };
        }

        // Magazine: [className, count, ammo]
        if (list.Count == 3 && list[0] is string)
        {
            return new ContainerItem
            {
                Type = "magazine",
                ClassName = ToSafeString(list[0]),
                Count = ToInt(list[1]),
                Ammo = ToInt(list[2])
            };
        }

        // Item: [className, count]
        if (list.Count == 2 && list[0] is string)
        {
            return new ContainerItem
            {
                Type = "item",
                ClassName = ToSafeString(list[0]),
                Count = ToInt(list[1])
            };
        }

        throw new FormatException(
            $"Unknown container item shape: count={list.Count}, types=[{string.Join(",", list.Select(e => e?.GetType().Name ?? "null"))}]"
        );
    }

    private static LinkedItems ParseLinkedItems(List<object> raw)
    {
        if (raw.Count < 6)
        {
            return new LinkedItems();
        }

        return new LinkedItems
        {
            Map = ToSafeString(raw[0]),
            Gps = ToSafeString(raw[1]),
            Radio = ToSafeString(raw[2]),
            Compass = ToSafeString(raw[3]),
            Watch = ToSafeString(raw[4]),
            Nvg = ToSafeString(raw[5])
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
            "weapon"    => [SerializeWeaponSlot(item.Weapon ?? new WeaponSlot()), (long)item.Count],
            "container" => [item.ClassName, item.IsBackpack ?? false],
            "magazine"  => [item.ClassName, (long)item.Count, (long)(item.Ammo ?? 0)],
            _           => [item.ClassName, (long)item.Count]
        };
    }
}
