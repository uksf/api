using System;
using System.Collections.Generic;
using System.Linq;
using UKSFWebsite.Api.Models.Mission;

namespace UKSFWebsite.Api.Services.Missions {
    public static class MissionEntityHelper {
        public static MissionEntity CreateFromItems(List<string> rawEntities) {
            MissionEntity missionEntity = new MissionEntity {itemsCount = Convert.ToInt32(MissionUtilities.ReadSingleDataByKey(rawEntities, "items"))};
            int index = rawEntities.FindIndex(x => x.Contains("class Item"));
            while (missionEntity.missionEntityItems.Count != missionEntity.itemsCount) {
                missionEntity.missionEntityItems.Add(MissionEntityItemHelper.CreateFromList(MissionUtilities.ReadDataFromIndex(rawEntities, ref index)));
            }

            return missionEntity;
        }

        private static MissionEntity CreateFromUnit(MissionUnit unit) {
            MissionEntity missionEntity = new MissionEntity();
            List<MissionPlayer> slots = MissionDataResolver.ResolveUnitSlots(unit);
            for (int i = 0; i < slots.Count; i++) {
                missionEntity.missionEntityItems.Add(MissionEntityItemHelper.CreateFromPlayer(slots[i], i));
            }

            return missionEntity;
        }

        public static void Patch(this MissionEntity missionEntity) {
            missionEntity.missionEntityItems.RemoveAll(x => x.itemType.Equals("Group") && x.missionEntity != null && x.missionEntity.missionEntityItems.All(y => y.isPlayable && !y.Ignored()));
            foreach (MissionUnit unit in MissionPatchData.instance.orderedUnits) {
                MissionEntity entity = CreateFromUnit(unit);
                missionEntity.missionEntityItems.Add(MissionEntityItemHelper.CreateFromMissionEntity(entity, unit.callsign));
            }

            missionEntity.itemsCount = missionEntity.missionEntityItems.Count;
            for (int index = 0; index < missionEntity.missionEntityItems.Count; index++) {
                MissionEntityItem item = missionEntity.missionEntityItems[index];
                item.Patch(index);
            }
        }

        public static IEnumerable<string> Serialize(this MissionEntity missionEntity) {
            missionEntity.itemsCount = missionEntity.missionEntityItems.Count;
            List<string> serialized = new List<string> {"class Entities", "{", $"items = {missionEntity.itemsCount};"};
            foreach (MissionEntityItem item in missionEntity.missionEntityItems) {
                serialized.AddRange(item.Serialize());
            }

            serialized.Add("};");
            return serialized;
        }
    }
}
