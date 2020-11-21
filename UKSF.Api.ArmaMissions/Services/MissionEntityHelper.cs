using System;
using System.Collections.Generic;
using System.Linq;
using UKSF.Api.ArmaMissions.Models;

namespace UKSF.Api.ArmaMissions.Services {
    public static class MissionEntityHelper {
        public static MissionEntity CreateFromItems(List<string> rawEntities) {
            MissionEntity missionEntity = new() { ItemsCount = Convert.ToInt32(MissionUtilities.ReadSingleDataByKey(rawEntities, "items")) };
            int index = rawEntities.FindIndex(x => x.Contains("class Item"));
            while (missionEntity.MissionEntityItems.Count != missionEntity.ItemsCount) {
                missionEntity.MissionEntityItems.Add(MissionEntityItemHelper.CreateFromList(MissionUtilities.ReadDataFromIndex(rawEntities, ref index)));
            }

            return missionEntity;
        }

        private static MissionEntity CreateFromUnit(MissionUnit unit) {
            MissionEntity missionEntity = new();
            List<MissionPlayer> slots = MissionDataResolver.ResolveUnitSlots(unit);
            for (int i = 0; i < slots.Count; i++) {
                missionEntity.MissionEntityItems.Add(MissionEntityItemHelper.CreateFromPlayer(slots[i], i));
            }

            return missionEntity;
        }

        public static void Patch(this MissionEntity missionEntity, int maxCurators) {
            MissionEntityItem.Position = 10;
            missionEntity.MissionEntityItems.RemoveAll(x => x.DataType.Equals("Group") && x.MissionEntity != null && x.MissionEntity.MissionEntityItems.All(y => y.IsPlayable && !y.Ignored()));
            foreach (MissionUnit unit in MissionPatchData.Instance.OrderedUnits) {
                missionEntity.MissionEntityItems.Add(MissionEntityItemHelper.CreateFromMissionEntity(CreateFromUnit(unit), unit.Callsign));
            }

            MissionEntityItem.CuratorPosition = 0.5;
            missionEntity.MissionEntityItems.RemoveAll(x => x.DataType == "Logic" && x.Type == "ModuleCurator_F");
            for (int index = 0; index < maxCurators; index++) {
                missionEntity.MissionEntityItems.Add(MissionEntityItemHelper.CreateCuratorEntity());
            }

            missionEntity.ItemsCount = missionEntity.MissionEntityItems.Count;
            for (int index = 0; index < missionEntity.MissionEntityItems.Count; index++) {
                missionEntity.MissionEntityItems[index].Patch(index);
            }
        }

        public static IEnumerable<string> Serialize(this MissionEntity missionEntity) {
            missionEntity.ItemsCount = missionEntity.MissionEntityItems.Count;
            List<string> serialized = new() { "class Entities", "{", $"items = {missionEntity.ItemsCount};" };
            foreach (MissionEntityItem item in missionEntity.MissionEntityItems) {
                serialized.AddRange(item.Serialize());
            }

            serialized.Add("};");
            return serialized;
        }
    }
}
