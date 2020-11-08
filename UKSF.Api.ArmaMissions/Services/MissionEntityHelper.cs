using System;
using System.Collections.Generic;
using System.Linq;
using UKSF.Api.ArmaMissions.Models;

namespace UKSF.Api.ArmaMissions.Services {
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

        public static void Patch(this MissionEntity missionEntity, int maxCurators) {
            MissionEntityItem.position = 10;
            missionEntity.missionEntityItems.RemoveAll(x => x.dataType.Equals("Group") && x.missionEntity != null && x.missionEntity.missionEntityItems.All(y => y.isPlayable && !y.Ignored()));
            foreach (MissionUnit unit in MissionPatchData.instance.orderedUnits) {
                missionEntity.missionEntityItems.Add(MissionEntityItemHelper.CreateFromMissionEntity(CreateFromUnit(unit), unit.callsign));
            }
            
            MissionEntityItem.curatorPosition = 0.5;
            missionEntity.missionEntityItems.RemoveAll(x => x.dataType == "Logic" && x.type == "ModuleCurator_F");
            for (int index = 0; index < maxCurators; index++) {
                missionEntity.missionEntityItems.Add(MissionEntityItemHelper.CreateCuratorEntity());
            }

            missionEntity.itemsCount = missionEntity.missionEntityItems.Count;
            for (int index = 0; index < missionEntity.missionEntityItems.Count; index++) {
                missionEntity.missionEntityItems[index].Patch(index);
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
