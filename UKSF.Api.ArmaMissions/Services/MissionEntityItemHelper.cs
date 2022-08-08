using UKSF.Api.ArmaMissions.Models;

namespace UKSF.Api.ArmaMissions.Services;

public static class MissionEntityItemHelper
{
    public static MissionEntityItem CreateFromList(List<string> rawItem)
    {
        MissionEntityItem missionEntityItem = new() { RawMissionEntityItem = rawItem };
        missionEntityItem.DataType = MissionUtilities.ReadSingleDataByKey(missionEntityItem.RawMissionEntityItem, "dataType").ToString();
        if (missionEntityItem.DataType.Equals("Group"))
        {
            missionEntityItem.RawMissionEntities = MissionUtilities.ReadDataByKey(missionEntityItem.RawMissionEntityItem, "Entities");
            if (missionEntityItem.RawMissionEntities.Count > 0)
            {
                missionEntityItem.MissionEntity = MissionEntityHelper.CreateFromItems(missionEntityItem.RawMissionEntities);
            }
        }
        else if (missionEntityItem.DataType.Equals("Object"))
        {
            var isPlayable = MissionUtilities.ReadSingleDataByKey(missionEntityItem.RawMissionEntityItem, "isPlayable").ToString();
            var isPlayer = MissionUtilities.ReadSingleDataByKey(missionEntityItem.RawMissionEntityItem, "isPlayer").ToString();
            if (!string.IsNullOrEmpty(isPlayable))
            {
                missionEntityItem.IsPlayable = isPlayable == "1";
            }
            else if (!string.IsNullOrEmpty(isPlayer))
            {
                missionEntityItem.IsPlayable = isPlayer == "1";
            }
        }
        else if (missionEntityItem.DataType.Equals("Logic"))
        {
            var type = MissionUtilities.ReadSingleDataByKey(missionEntityItem.RawMissionEntityItem, "type").ToString();
            if (!string.IsNullOrEmpty(type))
            {
                missionEntityItem.Type = type;
            }
        }

        return missionEntityItem;
    }

    public static MissionEntityItem CreateFromPlayer(MissionPlayer missionPlayer, int index)
    {
        MissionEntityItem missionEntityItem = new();
        missionEntityItem.RawMissionEntityItem.Add($"class Item{index}");
        missionEntityItem.RawMissionEntityItem.Add("{");
        missionEntityItem.RawMissionEntityItem.Add("dataType=\"Object\";");
        missionEntityItem.RawMissionEntityItem.Add($"flags={(index == 0 ? "7" : "5")};");
        missionEntityItem.RawMissionEntityItem.Add($"id={Mission.NextId++};");
        missionEntityItem.RawMissionEntityItem.Add("class PositionInfo");
        missionEntityItem.RawMissionEntityItem.Add("{");
        missionEntityItem.RawMissionEntityItem.Add("position[]={" + $"{MissionEntityItem.Position++}" + ",0,0};");
        missionEntityItem.RawMissionEntityItem.Add("};");
        missionEntityItem.RawMissionEntityItem.Add("side=\"West\";");
        missionEntityItem.RawMissionEntityItem.Add($"type=\"{missionPlayer.ObjectClass}\";");
        missionEntityItem.RawMissionEntityItem.Add("class Attributes");
        missionEntityItem.RawMissionEntityItem.Add("{");
        missionEntityItem.RawMissionEntityItem.Add("isPlayable=1;");
        missionEntityItem.RawMissionEntityItem.Add(
            $"description=\"{missionPlayer.Name}{(string.IsNullOrEmpty(missionPlayer.DomainAccount?.RoleAssignment) ? "" : $" - {missionPlayer.DomainAccount?.RoleAssignment}")}@{MissionDataResolver.ResolveCallsign(missionPlayer.Unit, missionPlayer.Unit.SourceUnit?.Callsign)}\";"
        );
        missionEntityItem.RawMissionEntityItem.Add("};");
        if (MissionDataResolver.IsEngineer(missionPlayer))
        {
            missionEntityItem.RawMissionEntityItem.AddEngineerTrait();
        }

        missionEntityItem.RawMissionEntityItem.Add("};");
        return missionEntityItem;
    }

    public static MissionEntityItem CreateFromMissionEntity(MissionEntity entities, string callsign)
    {
        MissionEntityItem missionEntityItem = new() { MissionEntity = entities };
        missionEntityItem.RawMissionEntityItem.Add("class Item");
        missionEntityItem.RawMissionEntityItem.Add("{");
        missionEntityItem.RawMissionEntityItem.Add("dataType=\"Group\";");
        missionEntityItem.RawMissionEntityItem.Add("side=\"West\";");
        missionEntityItem.RawMissionEntityItem.Add($"id={Mission.NextId++};");
        missionEntityItem.RawMissionEntityItem.Add("class Entities");
        missionEntityItem.RawMissionEntityItem.Add("{");
        missionEntityItem.RawMissionEntityItem.Add("};");
        missionEntityItem.RawMissionEntityItem.Add("class Attributes");
        missionEntityItem.RawMissionEntityItem.Add("{");
        missionEntityItem.RawMissionEntityItem.Add("};");
        missionEntityItem.RawMissionEntityItem.Add("class CustomAttributes");
        missionEntityItem.RawMissionEntityItem.Add("{");
        missionEntityItem.RawMissionEntityItem.Add("class Attribute0");
        missionEntityItem.RawMissionEntityItem.Add("{");
        missionEntityItem.RawMissionEntityItem.Add("property=\"groupID\";");
        missionEntityItem.RawMissionEntityItem.Add("expression=\"[_this, _value] call CBA_fnc_setCallsign\";");
        missionEntityItem.RawMissionEntityItem.Add("class Value");
        missionEntityItem.RawMissionEntityItem.Add("{");
        missionEntityItem.RawMissionEntityItem.Add("class data");
        missionEntityItem.RawMissionEntityItem.Add("{");
        missionEntityItem.RawMissionEntityItem.Add("class type");
        missionEntityItem.RawMissionEntityItem.Add("{");
        missionEntityItem.RawMissionEntityItem.Add("type[]={\"STRING\"};");
        missionEntityItem.RawMissionEntityItem.Add("};");
        missionEntityItem.RawMissionEntityItem.Add($"value=\"{callsign}\";");
        missionEntityItem.RawMissionEntityItem.Add("};");
        missionEntityItem.RawMissionEntityItem.Add("};");
        missionEntityItem.RawMissionEntityItem.Add("};");
        missionEntityItem.RawMissionEntityItem.Add("nAttributes=1;");
        missionEntityItem.RawMissionEntityItem.Add("};");
        missionEntityItem.RawMissionEntityItem.Add("};");
        missionEntityItem.RawMissionEntities = MissionUtilities.ReadDataByKey(missionEntityItem.RawMissionEntityItem, "Entities");
        return missionEntityItem;
    }

    public static MissionEntityItem CreateCuratorEntity()
    {
        MissionEntityItem missionEntityItem = new();
        missionEntityItem.RawMissionEntityItem.Add("class Item");
        missionEntityItem.RawMissionEntityItem.Add("{");
        missionEntityItem.RawMissionEntityItem.Add("dataType=\"Logic\";");
        missionEntityItem.RawMissionEntityItem.Add($"id={Mission.NextId++};");
        missionEntityItem.RawMissionEntityItem.Add("class PositionInfo");
        missionEntityItem.RawMissionEntityItem.Add("{");
        missionEntityItem.RawMissionEntityItem.Add("position[]={" + $"{MissionEntityItem.CuratorPosition++}" + ",0,0.25};");
        missionEntityItem.RawMissionEntityItem.Add("};");
        missionEntityItem.RawMissionEntityItem.Add("type=\"ModuleCurator_F\";");
        missionEntityItem.RawMissionEntityItem.Add("class CustomAttributes");
        missionEntityItem.RawMissionEntityItem.Add("{");
        missionEntityItem.RawMissionEntityItem.Add("};");
        missionEntityItem.RawMissionEntityItem.Add("};");
        return missionEntityItem;
    }

    public static bool Ignored(this MissionEntityItem missionEntityItem)
    {
        return missionEntityItem.RawMissionEntityItem.Any(x => x.ToLower().Contains("@ignore"));
    }

    public static void Patch(this MissionEntityItem missionEntityItem, int index)
    {
        missionEntityItem.RawMissionEntityItem[0] = $"class Item{index}";
    }

    public static IEnumerable<string> Serialize(this MissionEntityItem missionEntityItem)
    {
        if (missionEntityItem.RawMissionEntities.Count > 0)
        {
            var start = MissionUtilities.GetIndexByKey(missionEntityItem.RawMissionEntityItem, "Entities");
            var count = missionEntityItem.RawMissionEntities.Count;
            missionEntityItem.RawMissionEntityItem.RemoveRange(start, count);
            missionEntityItem.RawMissionEntityItem.InsertRange(start, missionEntityItem.MissionEntity.Serialize());
        }

        return missionEntityItem.RawMissionEntityItem.ToList();
    }

    private static void AddEngineerTrait(this ICollection<string> entity)
    {
        entity.Add("class CustomAttributes");
        entity.Add("{");
        entity.Add("class Attribute0");
        entity.Add("{");
        entity.Add("property=\"Enh_unitTraits_engineer\";");
        entity.Add("expression=\"_this setUnitTrait ['Engineer',_value]\";");
        entity.Add("class Value");
        entity.Add("{");
        entity.Add("class data");
        entity.Add("{");
        entity.Add("class type");
        entity.Add("{");
        entity.Add("type[]=");
        entity.Add("{");
        entity.Add("\"BOOL\"");
        entity.Add("};");
        entity.Add("};");
        entity.Add("value=1;");
        entity.Add("};");
        entity.Add("};");
        entity.Add("};");
        entity.Add("nAttributes=1;");
        entity.Add("};");
    }
}
