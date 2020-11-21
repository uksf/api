using System.Collections.Generic;
using System.Linq;
using MongoDB.Bson;
using UKSF.Api.Admin.Extensions;
using UKSF.Api.Admin.Services;
using UKSF.Api.ArmaMissions.Models;
using UKSF.Api.Personnel.Context;
using UKSF.Api.Personnel.Models;
using UKSF.Api.Personnel.Services;

namespace UKSF.Api.ArmaMissions.Services {
    public class MissionPatchDataService {
        private readonly IAccountContext _accountContext;
        private readonly IDisplayNameService _displayNameService;
        private readonly IRanksContext _ranksContext;
        private readonly IRanksService _ranksService;
        private readonly IUnitsContext _unitContext;
        private readonly IVariablesService _variablesService;

        public MissionPatchDataService(
            IRanksContext ranksContext,
            IAccountContext accountContext,
            IUnitsContext unitContext,
            IRanksService ranksService,
            IDisplayNameService displayNameService,
            IVariablesService variablesService
        ) {
            _ranksContext = ranksContext;
            _accountContext = accountContext;
            _unitContext = unitContext;
            _ranksService = ranksService;
            _displayNameService = displayNameService;
            _variablesService = variablesService;
        }

        public void UpdatePatchData() {
            MissionPatchData.Instance = new MissionPatchData {
                Units = new List<MissionUnit>(),
                Ranks = _ranksContext.Get().ToList(),
                Players = new List<MissionPlayer>(),
                OrderedUnits = new List<MissionUnit>(),
                MedicIds = _variablesService.GetVariable("MISSIONS_MEDIC_IDS").AsEnumerable(),
                EngineerIds = _variablesService.GetVariable("MISSIONS_ENGINEER_IDS").AsEnumerable()
            };

            foreach (Unit unit in _unitContext.Get(x => x.Branch == UnitBranch.COMBAT).ToList()) {
                MissionPatchData.Instance.Units.Add(new MissionUnit { SourceUnit = unit });
            }

            foreach (Account account in _accountContext.Get().Where(x => !string.IsNullOrEmpty(x.Rank) && _ranksService.IsSuperiorOrEqual(x.Rank, "Recruit"))) {
                MissionPatchData.Instance.Players.Add(new MissionPlayer { Account = account, Rank = _ranksContext.GetSingle(account.Rank), Name = _displayNameService.GetDisplayName(account) });
            }

            foreach (MissionUnit missionUnit in MissionPatchData.Instance.Units) {
                missionUnit.Callsign = MissionDataResolver.ResolveCallsign(missionUnit, missionUnit.SourceUnit.Callsign);
                missionUnit.Members = missionUnit.SourceUnit.Members.Select(x => MissionPatchData.Instance.Players.FirstOrDefault(y => y.Account.Id == x)).ToList();
                if (missionUnit.SourceUnit.Roles.Count > 0) {
                    missionUnit.Roles = missionUnit.SourceUnit.Roles.ToDictionary(pair => pair.Key, pair => MissionPatchData.Instance.Players.FirstOrDefault(y => y.Account.Id == pair.Value));
                }
            }

            foreach (MissionPlayer missionPlayer in MissionPatchData.Instance.Players) {
                missionPlayer.Unit = MissionPatchData.Instance.Units.Find(x => x.SourceUnit.Name == missionPlayer.Account.UnitAssignment);
                missionPlayer.ObjectClass = MissionDataResolver.ResolveObjectClass(missionPlayer);
            }

            MissionUnit parent = MissionPatchData.Instance.Units.First(x => x.SourceUnit.Parent == ObjectId.Empty.ToString());
            MissionPatchData.Instance.OrderedUnits.Add(parent);
            InsertUnitChildren(MissionPatchData.Instance.OrderedUnits, parent);
            MissionPatchData.Instance.OrderedUnits.RemoveAll(x => !MissionDataResolver.IsUnitPermanent(x) && x.Members.Count == 0 || string.IsNullOrEmpty(x.Callsign));
            MissionDataResolver.ResolveSpecialUnits(ref MissionPatchData.Instance.OrderedUnits);
        }

        private static void InsertUnitChildren(List<MissionUnit> newUnits, MissionUnit parent) {
            List<MissionUnit> children = MissionPatchData.Instance.Units.Where(x => x.SourceUnit.Parent == parent.SourceUnit.Id).OrderBy(x => x.SourceUnit.Order).ToList();
            if (children.Count == 0) return;
            int index = newUnits.IndexOf(parent);
            newUnits.InsertRange(index + 1, children);
            foreach (MissionUnit child in children) {
                InsertUnitChildren(newUnits, child);
            }
        }
    }
}
