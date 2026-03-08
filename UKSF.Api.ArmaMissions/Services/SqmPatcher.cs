using UKSF.Api.ArmaMissions.Models;
using UKSF.Api.ArmaMissions.Models.Sqm;

namespace UKSF.Api.ArmaMissions.Services;

public interface ISqmPatcher
{
    void Patch(MissionPatchContext context);
}

public class SqmPatcher : ISqmPatcher
{
    // Position offsets to space entities apart in the world so they don't stack on each other
    private const int PlayerStartPosition = 10;
    private const double CuratorStartPosition = 0.5;

    public void Patch(MissionPatchContext context)
    {
        var entities = context.Sqm.Entities;
        var position = PlayerStartPosition;

        entities.RemoveAll(e => e is SqmGroup { AllChildrenPlayable: true, IsIgnored: false });
        entities.RemoveAll(e => e is SqmLogic { Type: "ModuleCurator_F" });

        foreach (var unit in context.PatchData.OrderedUnits)
        {
            var group = CreateGroupEntity(unit, context, ref position);
            entities.Add(group);
        }

        var curatorPosition = CuratorStartPosition;
        for (var i = 0; i < context.MaxCurators; i++)
        {
            var curator = CreateCuratorEntity(context, ref curatorPosition);
            entities.Add(curator);
        }

        for (var i = 0; i < entities.Count; i++)
        {
            ReindexEntity(entities[i], i);
        }
    }

    private static SqmGroup CreateGroupEntity(PatchUnit unit, MissionPatchContext context, ref int position)
    {
        List<string> rawLines =
        [
            "class Item",
            "{",
            "dataType=\"Group\";",
            "side=\"West\";",
            $"id={context.NextEntityId++};",
            "class Entities",
            "{",
            "};",
            "class Attributes",
            "{",
            "};",
            "class CustomAttributes",
            "{",
            "class Attribute0",
            "{",
            "property=\"groupID\";",
            "expression=\"[_this, _value] call CBA_fnc_setCallsign\";",
            "class Value",
            "{",
            "class data",
            "{",
            "class type",
            "{",
            "type[]={\"STRING\"};",
            "};",
            $"value=\"{unit.Callsign}\";",
            "};",
            "};",
            "};",
            "nAttributes=1;",
            "};",
            "};"
        ];

        List<SqmEntity> children = [];
        for (var i = 0; i < unit.Slots.Count; i++)
        {
            children.Add(CreatePlayerEntity(unit.Slots[i], i, context, ref position));
        }

        return new SqmGroup
        {
            Children = children,
            AllChildrenPlayable = true,
            RawLines = rawLines
        };
    }

    private static SqmObject CreatePlayerEntity(PatchPlayer player, int index, MissionPatchContext context, ref int position)
    {
        var description = string.IsNullOrEmpty(player.RoleAssignment)
            ? $"{player.DisplayName}@{player.Callsign}"
            : $"{player.DisplayName} - {player.RoleAssignment}@{player.Callsign}";

        List<string> rawLines =
        [
            $"class Item{index}",
            "{",
            "dataType=\"Object\";",
            $"flags={(index == 0 ? "7" : "5")};",
            $"id={context.NextEntityId++};",
            "class PositionInfo",
            "{",
            $"position[]={{{position++},0,0}};",
            "};",
            "side=\"West\";",
            $"type=\"{player.ObjectClass}\";",
            "class Attributes",
            "{",
            "isPlayable=1;",
            $"description=\"{description}\";",
            "};"
        ];

        if (player.IsEngineer)
        {
            rawLines.AddRange(
                [
                    "class CustomAttributes",
                    "{",
                    "class Attribute0",
                    "{",
                    "property=\"Enh_unitTraits_engineer\";",
                    "expression=\"_this setUnitTrait ['Engineer',_value]\";",
                    "class Value",
                    "{",
                    "class data",
                    "{",
                    "class type",
                    "{",
                    "type[]=",
                    "{",
                    "\"BOOL\"",
                    "};",
                    "};",
                    "value=1;",
                    "};",
                    "};",
                    "};",
                    "nAttributes=1;",
                    "};"
                ]
            );
        }

        rawLines.Add("};");

        return new SqmObject
        {
            IsPlayable = true,
            Type = player.ObjectClass,
            RawLines = rawLines
        };
    }

    private static SqmLogic CreateCuratorEntity(MissionPatchContext context, ref double curatorPosition)
    {
        List<string> rawLines =
        [
            "class Item",
            "{",
            "dataType=\"Logic\";",
            $"id={context.NextEntityId++};",
            "class PositionInfo",
            "{",
            $"position[]={{{curatorPosition++},0,0.25}};",
            "};",
            "type=\"ModuleCurator_F\";",
            "class CustomAttributes",
            "{",
            "};",
            "};"
        ];

        return new SqmLogic { Type = "ModuleCurator_F", RawLines = rawLines };
    }

    private static void ReindexEntity(SqmEntity entity, int index)
    {
        if (entity.RawLines is { Count: > 0 })
        {
            entity.RawLines[0] = $"class Item{index}";
        }
    }
}
