using System.Collections.Generic;
using System.Linq;
using UKSFWebsite.Api.Models.Mission;

namespace UKSFWebsite.Api.Services.Missions {
    public static class MissionEntityItemHelper {
        public static MissionEntityItem CreateFromList(List<string> rawItem) {
            MissionEntityItem missionEntityItem = new MissionEntityItem {rawMissionEntityItem = rawItem};
            missionEntityItem.itemType = MissionUtilities.ReadSingleDataByKey(missionEntityItem.rawMissionEntityItem, "dataType").ToString();
            if (missionEntityItem.itemType.Equals("Group")) {
                missionEntityItem.rawMissionEntities = MissionUtilities.ReadDataByKey(missionEntityItem.rawMissionEntityItem, "Entities");
                if (missionEntityItem.rawMissionEntities.Count > 0) {
                    missionEntityItem.missionEntity = MissionEntityHelper.CreateFromItems(missionEntityItem.rawMissionEntities);
                }
            } else if (missionEntityItem.itemType.Equals("Object")) {
                string isPlayable = MissionUtilities.ReadSingleDataByKey(missionEntityItem.rawMissionEntityItem, "isPlayable").ToString();
                string isPlayer = MissionUtilities.ReadSingleDataByKey(missionEntityItem.rawMissionEntityItem, "isPlayer").ToString();
                if (!string.IsNullOrEmpty(isPlayable)) {
                    missionEntityItem.isPlayable = isPlayable == "1";
                } else if (!string.IsNullOrEmpty(isPlayer)) {
                    missionEntityItem.isPlayable = isPlayer == "1";
                }
            }

            return missionEntityItem;
        }

        public static MissionEntityItem CreateFromPlayer(MissionPlayer missionPlayer, int index) {
            MissionEntityItem missionEntityItem = new MissionEntityItem();
            missionEntityItem.rawMissionEntityItem.Add($"class Item{index}");
            missionEntityItem.rawMissionEntityItem.Add("{");
            missionEntityItem.rawMissionEntityItem.Add("dataType=\"Object\";");
            missionEntityItem.rawMissionEntityItem.Add($"flags={(index == 0 ? "7" : "5")};");
            missionEntityItem.rawMissionEntityItem.Add($"id={Mission.nextId++};");
            missionEntityItem.rawMissionEntityItem.Add("class PositionInfo");
            missionEntityItem.rawMissionEntityItem.Add("{");
            missionEntityItem.rawMissionEntityItem.Add("position[]={" + $"{MissionEntityItem.position += 1}" + ",0,0};");
            missionEntityItem.rawMissionEntityItem.Add("};");
            missionEntityItem.rawMissionEntityItem.Add("side=\"West\";");
            missionEntityItem.rawMissionEntityItem.Add($"type=\"{missionPlayer.objectClass}\";");
            missionEntityItem.rawMissionEntityItem.Add("class Attributes");
            missionEntityItem.rawMissionEntityItem.Add("{");
            missionEntityItem.rawMissionEntityItem.Add("isPlayable=1;");
            missionEntityItem.rawMissionEntityItem.Add(
                $"description=\"{missionPlayer.name}{(string.IsNullOrEmpty(missionPlayer.account?.roleAssignment) ? "" : $" - {missionPlayer.account?.roleAssignment}")}@{MissionDataResolver.ResolveCallsign(missionPlayer.unit, missionPlayer.unit.sourceUnit?.callsign)}\";"
            );
            missionEntityItem.rawMissionEntityItem.Add("};");
            if (MissionDataResolver.IsEngineer(missionPlayer)) missionEntityItem.rawMissionEntityItem.AddEngineerTrait();
            missionEntityItem.rawMissionEntityItem.Add("};");
            return missionEntityItem;
        }

        public static MissionEntityItem CreateFromMissionEntity(MissionEntity entities, string callsign) {
            MissionEntityItem missionEntityItem = new MissionEntityItem {missionEntity = entities};
            missionEntityItem.rawMissionEntityItem.Add("class Item");
            missionEntityItem.rawMissionEntityItem.Add("{");
            missionEntityItem.rawMissionEntityItem.Add("dataType=\"Group\";");
            missionEntityItem.rawMissionEntityItem.Add("side=\"West\";");
            missionEntityItem.rawMissionEntityItem.Add($"id={Mission.nextId++};");
            missionEntityItem.rawMissionEntityItem.Add("class Entities");
            missionEntityItem.rawMissionEntityItem.Add("{");
            missionEntityItem.rawMissionEntityItem.Add("};");
            missionEntityItem.rawMissionEntityItem.Add("class Attributes");
            missionEntityItem.rawMissionEntityItem.Add("{");
            missionEntityItem.rawMissionEntityItem.Add("};");
            missionEntityItem.rawMissionEntityItem.Add("class CustomAttributes");
            missionEntityItem.rawMissionEntityItem.Add("{");
            missionEntityItem.rawMissionEntityItem.Add("class Attribute0");
            missionEntityItem.rawMissionEntityItem.Add("{");
            missionEntityItem.rawMissionEntityItem.Add("property=\"groupID\";");
            missionEntityItem.rawMissionEntityItem.Add("expression=\"[_this, _value] call CBA_fnc_setCallsign\";");
            missionEntityItem.rawMissionEntityItem.Add("class Value");
            missionEntityItem.rawMissionEntityItem.Add("{");
            missionEntityItem.rawMissionEntityItem.Add("class data");
            missionEntityItem.rawMissionEntityItem.Add("{");
            missionEntityItem.rawMissionEntityItem.Add("class type");
            missionEntityItem.rawMissionEntityItem.Add("{");
            missionEntityItem.rawMissionEntityItem.Add("type[]={\"STRING\"};");
            missionEntityItem.rawMissionEntityItem.Add("};");
            missionEntityItem.rawMissionEntityItem.Add($"value=\"{callsign}\";");
            missionEntityItem.rawMissionEntityItem.Add("};");
            missionEntityItem.rawMissionEntityItem.Add("};");
            missionEntityItem.rawMissionEntityItem.Add("};");
            missionEntityItem.rawMissionEntityItem.Add("nAttributes=1;");
            missionEntityItem.rawMissionEntityItem.Add("};");
            missionEntityItem.rawMissionEntityItem.Add("};");
            missionEntityItem.rawMissionEntities = MissionUtilities.ReadDataByKey(missionEntityItem.rawMissionEntityItem, "Entities");
            return missionEntityItem;
        }

        public static bool Ignored(this MissionEntityItem missionEntityItem) {
            return missionEntityItem.rawMissionEntityItem.Any(x => x.ToLower().Contains("@ignore"));
        }

        public static void Patch(this MissionEntityItem missionEntityItem, int index) {
            missionEntityItem.rawMissionEntityItem[0] = $"class Item{index}";
        }

        public static IEnumerable<string> Serialize(this MissionEntityItem missionEntityItem) {
            if (missionEntityItem.rawMissionEntities.Count > 0) {
                int start = MissionUtilities.GetIndexByKey(missionEntityItem.rawMissionEntityItem, "Entities");
                int count = missionEntityItem.rawMissionEntities.Count;
                missionEntityItem.rawMissionEntityItem.RemoveRange(start, count);
                missionEntityItem.rawMissionEntityItem.InsertRange(start, missionEntityItem.missionEntity.Serialize());
            }

            return missionEntityItem.rawMissionEntityItem.ToList();
        }
        
        private static void AddEngineerTrait(this ICollection<string> entity) {
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
}
