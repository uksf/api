using MongoDB.Bson;
using UKSF.Api.ArmaMissions.Models;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Models.Domain;
using UKSF.Api.Core.Services;

namespace UKSF.Api.ArmaMissions.Services;

public interface IPatchDataBuilder
{
    void Build(MissionPatchContext context);
}

public class PatchDataBuilder(
    IRanksContext ranksContext,
    IAccountContext accountContext,
    IUnitsContext unitContext,
    IRanksService ranksService,
    IDisplayNameService displayNameService
) : IPatchDataBuilder
{
    public void Build(MissionPatchContext context)
    {
        var ranks = ranksContext.Get().ToList();

        var units = unitContext.Get(x => x.Branch == UnitBranch.Combat).Select(u => new InternalUnit { Source = u }).ToList();

        var players = accountContext.Get()
                                    .Where(x => !string.IsNullOrEmpty(x.Rank) && ranksService.IsSuperiorOrEqual(x.Rank, "Recruit"))
                                    .Select(a => new InternalPlayer
                                        {
                                            Account = a,
                                            Rank = ranks.Find(r => r.Name == a.Rank),
                                            DisplayName = displayNameService.GetDisplayName(a)
                                        }
                                    )
                                    .ToList();

        foreach (var unit in units)
        {
            unit.Members = unit.Source.Members.Select(id => players.FirstOrDefault(p => p.Account.Id == id)).ToList();
        }

        var parent = units.FirstOrDefault(u => u.Source.Parent == ObjectId.Empty.ToString()) ??
                     throw new InvalidOperationException("No root unit found with empty parent ID");
        List<InternalUnit> orderedUnits = [parent];
        InsertUnitChildren(orderedUnits, parent, units);

        orderedUnits.RemoveAll(u => (!IsUnitPermanent(u) && u.Members.Count == 0) || string.IsNullOrEmpty(ResolveCallsign(u)));

        AggregateSpecialUnits(orderedUnits);

        var patchUnits = orderedUnits.Select(u => BuildPatchUnit(u, ranks)).ToList();

        context.PatchData = new PatchData { Ranks = ranks, OrderedUnits = patchUnits };
    }

    private static PatchUnit BuildPatchUnit(InternalUnit unit, List<DomainRank> ranks)
    {
        var callsign = ResolveCallsign(unit);
        var slots = ResolveSlots(unit, ranks);
        SortSlots(slots, unit, ranks);

        return new PatchUnit
        {
            Source = unit.Source,
            Callsign = callsign,
            Slots = slots.Select(p => ToPatchPlayer(p, unit)).ToList()
        };
    }

    private static PatchPlayer ToPatchPlayer(InternalPlayer player, InternalUnit unit)
    {
        var objectClass = ResolveObjectClass(player, unit);
        var callsign = ResolveCallsign(unit);
        return new PatchPlayer
        {
            DisplayName = player.DisplayName,
            ObjectClass = objectClass,
            RoleAssignment = player.Account?.RoleAssignment,
            Callsign = callsign,
            IsMedic = player.Account?.Qualifications?.Medic ?? false,
            IsEngineer = player.Account?.Qualifications?.Engineer ?? false,
            Rank = player.Rank
        };
    }

    private static string ResolveObjectClass(InternalPlayer player, InternalUnit unit)
    {
        var settings = unit.Source.MissionPatchSettings;

        if (settings?.IsPilotUnit == true)
        {
            return "UKSF_B_Pilot";
        }

        if (!string.IsNullOrEmpty(settings?.ForcedObjectClass))
        {
            return settings.ForcedObjectClass;
        }

        if (player.Account?.Qualifications?.Medic == true)
        {
            return "UKSF_B_Medic";
        }

        if (GetChainOfCommandSortPriority(player, unit) != -1)
        {
            return "UKSF_B_SectionLeader";
        }

        return "UKSF_B_Rifleman";
    }

    private static List<InternalPlayer> ResolveSlots(InternalUnit unit, List<DomainRank> ranks)
    {
        var settings = unit.Source.MissionPatchSettings;
        List<InternalPlayer> slots = [];

        if (settings?.AggregateIntoParent == true)
        {
            return slots;
        }

        slots.AddRange(unit.Members);

        if (settings is { MaxSlots: > 0 })
        {
            var fillerCount = settings.MaxSlots - slots.Count;
            for (var i = 0; i < fillerCount; i++)
            {
                slots.Add(
                    new InternalPlayer { DisplayName = settings.FillerName ?? "Reserve", Rank = ranks.Find(r => r.Name == (settings.FillerRank ?? "Recruit")) }
                );
            }
        }

        return slots;
    }

    private static void AggregateSpecialUnits(List<InternalUnit> orderedUnits)
    {
        var toRemove = new List<InternalUnit>();

        foreach (var unit in orderedUnits.ToList())
        {
            var settings = unit.Source.MissionPatchSettings;
            if (settings is { AggregateIntoParent: true })
            {
                var parent = orderedUnits.FirstOrDefault(u => u.Source.Id == unit.Source.Parent);
                if (parent != null)
                {
                    parent.Members.AddRange(unit.Members);
                }

                toRemove.Add(unit);
            }
            else if (settings is { Pruned: true })
            {
                toRemove.Add(unit);
            }
        }

        orderedUnits.RemoveAll(toRemove.Contains);
    }

    private static string ResolveCallsign(InternalUnit unit)
    {
        if (unit.Source.MissionPatchSettings?.IsPilotUnit == true)
        {
            return "JSFAW";
        }

        return unit.Source.Callsign;
    }

    private static bool IsUnitPermanent(InternalUnit unit)
    {
        return unit.Source.MissionPatchSettings?.IsPermanent == true;
    }

    private static int GetChainOfCommandSortPriority(InternalPlayer player, InternalUnit unit)
    {
        var chainOfCommand = unit.Source.ChainOfCommand;
        if (chainOfCommand == null || player.Account?.Id == null)
        {
            return -1;
        }

        var playerId = player.Account.Id;
        if (chainOfCommand.First == playerId) return 3;
        if (chainOfCommand.Second == playerId) return 2;
        if (chainOfCommand.Third == playerId) return 1;
        if (chainOfCommand.Nco == playerId) return 0;
        return -1;
    }

    private static void SortSlots(List<InternalPlayer> slots, InternalUnit unit, List<DomainRank> ranks)
    {
        slots.Sort((a, b) =>
            {
                var priorityA = GetChainOfCommandSortPriority(a, unit);
                var priorityB = GetChainOfCommandSortPriority(b, unit);
                var rankA = ranks.IndexOf(a.Rank);
                var rankB = ranks.IndexOf(b.Rank);
                return priorityA < priorityB ? 1 :
                    priorityA > priorityB    ? -1 :
                    rankA < rankB            ? -1 :
                    rankA > rankB            ? 1 : string.CompareOrdinal(a.DisplayName, b.DisplayName);
            }
        );
    }

    private static void InsertUnitChildren(List<InternalUnit> newUnits, InternalUnit parent, List<InternalUnit> allUnits)
    {
        var children = allUnits.Where(u => u.Source.Parent == parent.Source.Id).OrderBy(u => u.Source.Order).ToList();
        if (children.Count == 0)
        {
            return;
        }

        var index = newUnits.IndexOf(parent);
        newUnits.InsertRange(index + 1, children);
        foreach (var child in children)
        {
            InsertUnitChildren(newUnits, child, allUnits);
        }
    }

    private class InternalUnit
    {
        public DomainUnit Source { get; init; }
        public List<InternalPlayer> Members { get; set; } = [];
    }

    private class InternalPlayer
    {
        public DomainAccount Account { get; init; }
        public string DisplayName { get; init; }
        public DomainRank Rank { get; init; }
    }
}
