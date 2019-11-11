using System.Collections.Generic;
using System.Linq;
using MongoDB.Bson;
using UKSFWebsite.Api.Interfaces.Personnel;
using UKSFWebsite.Api.Interfaces.Units;
using UKSFWebsite.Api.Models.Mission;
using UKSFWebsite.Api.Models.Personnel;
using UKSFWebsite.Api.Models.Units;

namespace UKSFWebsite.Api.Services.Game.Missions {
    public class MissionPatchDataService {
        private readonly IAccountService accountService;
        private readonly IDisplayNameService displayNameService;
        private readonly IRanksService ranksService;
        private readonly IUnitsService unitsService;

        public MissionPatchDataService(IRanksService ranksService, IUnitsService unitsService, IAccountService accountService, IDisplayNameService displayNameService) {
            this.ranksService = ranksService;
            this.unitsService = unitsService;
            this.accountService = accountService;
            this.displayNameService = displayNameService;
        }

        public void UpdatePatchData() {
            MissionPatchData.instance = new MissionPatchData {units = new List<MissionUnit>(), ranks = ranksService.Data().Get(), players = new List<MissionPlayer>(), orderedUnits = new List<MissionUnit>()};

            foreach (Unit unit in unitsService.Data().Get(x => x.branch == UnitBranch.COMBAT).ToList()) {
                MissionPatchData.instance.units.Add(new MissionUnit {sourceUnit = unit, depth = unitsService.GetUnitDepth(unit)});
            }

            foreach (Account account in accountService.Data().Get().Where(x => !string.IsNullOrEmpty(x.rank) && ranksService.IsSuperiorOrEqual(x.rank, "Recruit"))) {
                MissionPatchData.instance.players.Add(new MissionPlayer {account = account, rank = ranksService.Data().GetSingle(account.rank), name = displayNameService.GetDisplayName(account)});
            }

            foreach (MissionUnit missionUnit in MissionPatchData.instance.units) {
                missionUnit.callsign = MissionDataResolver.ResolveCallsign(missionUnit, missionUnit.sourceUnit.callsign);
                missionUnit.members = missionUnit.sourceUnit.members.Select(x => MissionPatchData.instance.players.FirstOrDefault(y => y.account.id == x)).ToList();
                if (missionUnit.sourceUnit.roles.Count > 0) {
                    missionUnit.roles = missionUnit.sourceUnit.roles.ToDictionary(pair => pair.Key, pair => MissionPatchData.instance.players.FirstOrDefault(y => y.account.id == pair.Value));
                }
            }

            foreach (MissionPlayer missionPlayer in MissionPatchData.instance.players) {
                missionPlayer.unit = MissionPatchData.instance.units.Find(x => x.sourceUnit.name == missionPlayer.account.unitAssignment);
                missionPlayer.objectClass = MissionDataResolver.ResolveObjectClass(missionPlayer);
            }

            MissionUnit parent = MissionPatchData.instance.units.First(x => x.sourceUnit.parent == ObjectId.Empty.ToString());
            MissionPatchData.instance.orderedUnits.Add(parent);
            InsertUnitChildren(MissionPatchData.instance.orderedUnits, parent);
            MissionPatchData.instance.orderedUnits.RemoveAll(x => !MissionDataResolver.IsUnitPermanent(x) && x.members.Count == 0 || string.IsNullOrEmpty(x.callsign));
            MissionDataResolver.ResolveSpecialUnits(ref MissionPatchData.instance.orderedUnits);
        }

        private static void InsertUnitChildren(List<MissionUnit> newUnits, MissionUnit parent) {
            List<MissionUnit> children = MissionPatchData.instance.units.Where(x => x.sourceUnit.parent == parent.sourceUnit.id).OrderBy(x => x.sourceUnit.order).ToList();
            if (children.Count == 0) return;
            int index = newUnits.IndexOf(parent);
            newUnits.InsertRange(index + 1, children);
            foreach (MissionUnit child in children) {
                InsertUnitChildren(newUnits, child);
            }
        }
    }
}
