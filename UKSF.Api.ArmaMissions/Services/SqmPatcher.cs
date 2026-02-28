using UKSF.Api.ArmaMissions.Models;
using UKSF.Api.ArmaMissions.Models.Sqm;

namespace UKSF.Api.ArmaMissions.Services;

public interface ISqmPatcher
{
    void Patch(MissionPatchContext context);
}

public class SqmPatcher : ISqmPatcher
{
    public void Patch(MissionPatchContext context)
    {
        var entities = context.Sqm.Entities;
        var position = 10;

        // Remove all-playable groups (but not @ignore ones)
        entities.RemoveAll(e => e is SqmGroup { AllChildrenPlayable: true, IsIgnored: false });

        // Remove existing curator Logic entities
        entities.RemoveAll(e => e is SqmLogic { Type: "ModuleCurator_F" });

        // Add new groups for each unit
        foreach (var unit in context.PatchData.OrderedUnits)
        {
            var group = CreateGroupEntity(unit, context, ref position);
            entities.Add(group);
        }

        // Add curator entities
        var curatorPosition = 0.5;
        for (var i = 0; i < context.MaxCurators; i++)
        {
            var curator = CreateCuratorEntity(context, ref curatorPosition);
            entities.Add(curator);
        }

        // Re-index all items as Item0, Item1, ...
        for (var i = 0; i < entities.Count; i++)
        {
            ReindexEntity(entities[i], i);
        }
    }

    private static SqmGroup CreateGroupEntity(PatchUnit unit, MissionPatchContext context, ref int position)
    {
        List<string> rawLines = [];
        rawLines.Add("class Item");
        rawLines.Add("{");
        rawLines.Add("dataType=\"Group\";");
        rawLines.Add("side=\"West\";");
        rawLines.Add($"id={context.NextEntityId++};");
        rawLines.Add("class Entities");
        rawLines.Add("{");
        rawLines.Add("};");
        rawLines.Add("class Attributes");
        rawLines.Add("{");
        rawLines.Add("};");
        rawLines.Add("class CustomAttributes");
        rawLines.Add("{");
        rawLines.Add("class Attribute0");
        rawLines.Add("{");
        rawLines.Add("property=\"groupID\";");
        rawLines.Add("expression=\"[_this, _value] call CBA_fnc_setCallsign\";");
        rawLines.Add("class Value");
        rawLines.Add("{");
        rawLines.Add("class data");
        rawLines.Add("{");
        rawLines.Add("class type");
        rawLines.Add("{");
        rawLines.Add("type[]={\"STRING\"};");
        rawLines.Add("};");
        rawLines.Add($"value=\"{unit.Callsign}\";");
        rawLines.Add("};");
        rawLines.Add("};");
        rawLines.Add("};");
        rawLines.Add("nAttributes=1;");
        rawLines.Add("};");
        rawLines.Add("};");

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

        List<string> rawLines = [];
        rawLines.Add($"class Item{index}");
        rawLines.Add("{");
        rawLines.Add("dataType=\"Object\";");
        rawLines.Add($"flags={(index == 0 ? "7" : "5")};");
        rawLines.Add($"id={context.NextEntityId++};");
        rawLines.Add("class PositionInfo");
        rawLines.Add("{");
        rawLines.Add($"position[]={{{position++},0,0}};");
        rawLines.Add("};");
        rawLines.Add("side=\"West\";");
        rawLines.Add($"type=\"{player.ObjectClass}\";");
        rawLines.Add("class Attributes");
        rawLines.Add("{");
        rawLines.Add("isPlayable=1;");
        rawLines.Add($"description=\"{description}\";");
        rawLines.Add("};");

        if (player.IsEngineer)
        {
            rawLines.Add("class CustomAttributes");
            rawLines.Add("{");
            rawLines.Add("class Attribute0");
            rawLines.Add("{");
            rawLines.Add("property=\"Enh_unitTraits_engineer\";");
            rawLines.Add("expression=\"_this setUnitTrait ['Engineer',_value]\";");
            rawLines.Add("class Value");
            rawLines.Add("{");
            rawLines.Add("class data");
            rawLines.Add("{");
            rawLines.Add("class type");
            rawLines.Add("{");
            rawLines.Add("type[]=");
            rawLines.Add("{");
            rawLines.Add("\"BOOL\"");
            rawLines.Add("};");
            rawLines.Add("};");
            rawLines.Add("value=1;");
            rawLines.Add("};");
            rawLines.Add("};");
            rawLines.Add("};");
            rawLines.Add("nAttributes=1;");
            rawLines.Add("};");
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
        List<string> rawLines = [];
        rawLines.Add("class Item");
        rawLines.Add("{");
        rawLines.Add("dataType=\"Logic\";");
        rawLines.Add($"id={context.NextEntityId++};");
        rawLines.Add("class PositionInfo");
        rawLines.Add("{");
        rawLines.Add($"position[]={{{curatorPosition++},0,0.25}};");
        rawLines.Add("};");
        rawLines.Add("type=\"ModuleCurator_F\";");
        rawLines.Add("class CustomAttributes");
        rawLines.Add("{");
        rawLines.Add("};");
        rawLines.Add("};");

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
