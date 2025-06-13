using MongoDB.Bson;
using UKSF.Api.ArmaMissions.Models;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Core.Services;

namespace UKSF.Api.ArmaMissions.Services;

public class MissionPatchDataService(
    IRanksContext ranksContext,
    IAccountContext accountContext,
    IUnitsContext unitContext,
    IRanksService ranksService,
    IDisplayNameService displayNameService
)
{
    public void UpdatePatchData()
    {
        MissionPatchData.Instance = new MissionPatchData
        {
            Units = new List<MissionUnit>(),
            Ranks = ranksContext.Get().ToList(),
            Players = new List<MissionPlayer>(),
            OrderedUnits = new List<MissionUnit>()
        };

        foreach (var unit in unitContext.Get(x => x.Branch == UnitBranch.Combat).ToList())
        {
            MissionPatchData.Instance.Units.Add(new MissionUnit { SourceUnit = unit });
        }

        foreach (var account in accountContext.Get().Where(x => !string.IsNullOrEmpty(x.Rank) && ranksService.IsSuperiorOrEqual(x.Rank, "Recruit")))
        {
            MissionPatchData.Instance.Players.Add(
                new MissionPlayer
                {
                    Account = account,
                    Rank = ranksContext.GetSingle(account.Rank),
                    Name = displayNameService.GetDisplayName(account)
                }
            );
        }

        foreach (var missionUnit in MissionPatchData.Instance.Units)
        {
            missionUnit.Callsign = MissionDataResolver.ResolveCallsign(missionUnit, missionUnit.SourceUnit.Callsign);
            missionUnit.Members = missionUnit.SourceUnit.Members.Select(x => MissionPatchData.Instance.Players.FirstOrDefault(y => y.Account.Id == x)).ToList();
            if (missionUnit.SourceUnit.ChainOfCommand != null)
            {
                missionUnit.Roles = new Dictionary<string, MissionPlayer>();
                var chainOfCommand = missionUnit.SourceUnit.ChainOfCommand;

                if (!string.IsNullOrEmpty(chainOfCommand.First))
                {
                    missionUnit.Roles["1iC"] = MissionPatchData.Instance.Players.FirstOrDefault(y => y.Account.Id == chainOfCommand.First);
                }

                if (!string.IsNullOrEmpty(chainOfCommand.Second))
                {
                    missionUnit.Roles["2iC"] = MissionPatchData.Instance.Players.FirstOrDefault(y => y.Account.Id == chainOfCommand.Second);
                }

                if (!string.IsNullOrEmpty(chainOfCommand.Third))
                {
                    missionUnit.Roles["3iC"] = MissionPatchData.Instance.Players.FirstOrDefault(y => y.Account.Id == chainOfCommand.Third);
                }

                if (!string.IsNullOrEmpty(chainOfCommand.Nco))
                {
                    missionUnit.Roles["NCOiC"] = MissionPatchData.Instance.Players.FirstOrDefault(y => y.Account.Id == chainOfCommand.Nco);
                }
            }
        }

        foreach (var missionPlayer in MissionPatchData.Instance.Players)
        {
            missionPlayer.Unit = MissionPatchData.Instance.Units.Find(x => x.SourceUnit.Name == missionPlayer.Account.UnitAssignment);
            missionPlayer.ObjectClass = MissionDataResolver.ResolveObjectClass(missionPlayer);
        }

        var parent = MissionPatchData.Instance.Units.First(x => x.SourceUnit.Parent == ObjectId.Empty.ToString());
        MissionPatchData.Instance.OrderedUnits.Add(parent);
        InsertUnitChildren(MissionPatchData.Instance.OrderedUnits, parent);
        MissionPatchData.Instance.OrderedUnits.RemoveAll(x => (!MissionDataResolver.IsUnitPermanent(x) && x.Members.Count == 0) ||
                                                              string.IsNullOrEmpty(x.Callsign)
        );
        MissionDataResolver.ResolveSpecialUnits(MissionPatchData.Instance.OrderedUnits);
    }

    private static void InsertUnitChildren(List<MissionUnit> newUnits, MissionUnit parent)
    {
        var children = MissionPatchData.Instance.Units.Where(x => x.SourceUnit.Parent == parent.SourceUnit.Id).OrderBy(x => x.SourceUnit.Order).ToList();
        if (children.Count == 0)
        {
            return;
        }

        var index = newUnits.IndexOf(parent);
        newUnits.InsertRange(index + 1, children);
        foreach (var child in children)
        {
            InsertUnitChildren(newUnits, child);
        }
    }
}
