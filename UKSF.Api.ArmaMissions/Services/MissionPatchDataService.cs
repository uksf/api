using MongoDB.Bson;
using UKSF.Api.ArmaMissions.Models;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Models;
using UKSF.Api.Core.Services;

namespace UKSF.Api.ArmaMissions.Services;

public class MissionPatchDataService
{
    private readonly IAccountContext _accountContext;
    private readonly IDisplayNameService _displayNameService;
    private readonly IRanksContext _ranksContext;
    private readonly IRanksService _ranksService;
    private readonly IUnitsContext _unitContext;

    public MissionPatchDataService(
        IRanksContext ranksContext,
        IAccountContext accountContext,
        IUnitsContext unitContext,
        IRanksService ranksService,
        IDisplayNameService displayNameService
    )
    {
        _ranksContext = ranksContext;
        _accountContext = accountContext;
        _unitContext = unitContext;
        _ranksService = ranksService;
        _displayNameService = displayNameService;
    }

    public void UpdatePatchData()
    {
        MissionPatchData.Instance = new MissionPatchData
        {
            Units = new List<MissionUnit>(),
            Ranks = _ranksContext.Get().ToList(),
            Players = new List<MissionPlayer>(),
            OrderedUnits = new List<MissionUnit>()
        };

        foreach (var unit in _unitContext.Get(x => x.Branch == UnitBranch.COMBAT).ToList())
        {
            MissionPatchData.Instance.Units.Add(new MissionUnit { SourceUnit = unit });
        }

        foreach (var account in _accountContext.Get().Where(x => !string.IsNullOrEmpty(x.Rank) && _ranksService.IsSuperiorOrEqual(x.Rank, "Recruit")))
        {
            MissionPatchData.Instance.Players.Add(
                new MissionPlayer
                {
                    DomainAccount = account,
                    Rank = _ranksContext.GetSingle(account.Rank),
                    Name = _displayNameService.GetDisplayName(account)
                }
            );
        }

        foreach (var missionUnit in MissionPatchData.Instance.Units)
        {
            missionUnit.Callsign = MissionDataResolver.ResolveCallsign(missionUnit, missionUnit.SourceUnit.Callsign);
            missionUnit.Members = missionUnit.SourceUnit.Members.Select(x => MissionPatchData.Instance.Players.FirstOrDefault(y => y.DomainAccount.Id == x))
                                             .ToList();
            if (missionUnit.SourceUnit.Roles.Count > 0)
            {
                missionUnit.Roles = missionUnit.SourceUnit.Roles.ToDictionary(
                    pair => pair.Key,
                    pair => MissionPatchData.Instance.Players.FirstOrDefault(y => y.DomainAccount.Id == pair.Value)
                );
            }
        }

        foreach (var missionPlayer in MissionPatchData.Instance.Players)
        {
            missionPlayer.Unit = MissionPatchData.Instance.Units.Find(x => x.SourceUnit.Name == missionPlayer.DomainAccount.UnitAssignment);
            missionPlayer.ObjectClass = MissionDataResolver.ResolveObjectClass(missionPlayer);
        }

        var parent = MissionPatchData.Instance.Units.First(x => x.SourceUnit.Parent == ObjectId.Empty.ToString());
        MissionPatchData.Instance.OrderedUnits.Add(parent);
        InsertUnitChildren(MissionPatchData.Instance.OrderedUnits, parent);
        MissionPatchData.Instance.OrderedUnits.RemoveAll(
            x => (!MissionDataResolver.IsUnitPermanent(x) && x.Members.Count == 0) || string.IsNullOrEmpty(x.Callsign)
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
