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
    private List<DomainRank> _ranks;
    private List<InternalPlayer> _players;
    private List<InternalUnit> _units;

    public void Build(MissionPatchContext context)
    {
        _ranks = ranksContext.Get().ToList();

        _units = unitContext.Get(x => x.Branch == UnitBranch.Combat).Select(u => new InternalUnit { Source = u }).ToList();

        _players = accountContext.Get()
                                 .Where(x => !string.IsNullOrEmpty(x.Rank) && ranksService.IsSuperiorOrEqual(x.Rank, "Recruit"))
                                 .Select(a => new InternalPlayer
                                     {
                                         Account = a,
                                         Rank = ranksContext.GetSingle(a.Rank),
                                         DisplayName = displayNameService.GetDisplayName(a)
                                     }
                                 )
                                 .ToList();

        foreach (var unit in _units)
        {
            unit.Members = unit.Source.Members.Select(id => _players.FirstOrDefault(p => p.Account.Id == id)).ToList();

            if (unit.Source.ChainOfCommand != null)
            {
                var coc = unit.Source.ChainOfCommand;
                if (!string.IsNullOrEmpty(coc.First))
                {
                    unit.Roles["1iC"] = _players.FirstOrDefault(p => p.Account.Id == coc.First);
                }

                if (!string.IsNullOrEmpty(coc.Second))
                {
                    unit.Roles["2iC"] = _players.FirstOrDefault(p => p.Account.Id == coc.Second);
                }

                if (!string.IsNullOrEmpty(coc.Third))
                {
                    unit.Roles["3iC"] = _players.FirstOrDefault(p => p.Account.Id == coc.Third);
                }

                if (!string.IsNullOrEmpty(coc.Nco))
                {
                    unit.Roles["NCOiC"] = _players.FirstOrDefault(p => p.Account.Id == coc.Nco);
                }
            }
        }

        foreach (var player in _players)
        {
            player.Unit = _units.Find(u => u.Source.Name == player.Account.UnitAssignment);
        }

        var parent = _units.First(u => u.Source.Parent == ObjectId.Empty.ToString());
        List<InternalUnit> orderedUnits = [parent];
        InsertUnitChildren(orderedUnits, parent);

        orderedUnits.RemoveAll(u => (!IsUnitPermanent(u) && u.Members.Count == 0) || string.IsNullOrEmpty(ResolveCallsign(u)));

        AggregateSpecialUnits(orderedUnits);

        var patchUnits = orderedUnits.Select(u => BuildPatchUnit(u)).ToList();

        context.PatchData = new PatchData { Ranks = _ranks, OrderedUnits = patchUnits };
    }

    private PatchUnit BuildPatchUnit(InternalUnit unit)
    {
        var callsign = ResolveCallsign(unit);
        var slots = ResolveSlots(unit);
        SortSlots(slots, unit);

        return new PatchUnit
        {
            Source = unit.Source,
            Callsign = callsign,
            Slots = slots.Select(p => ToPatchPlayer(p, unit, callsign)).ToList()
        };
    }

    private PatchPlayer ToPatchPlayer(InternalPlayer player, InternalUnit unit, string callsign)
    {
        var objectClass = ResolveObjectClass(player, unit);
        var playerCallsign = ResolveCallsign(unit);
        return new PatchPlayer
        {
            DisplayName = player.DisplayName,
            ObjectClass = objectClass,
            RoleAssignment = player.Account?.RoleAssignment,
            Callsign = playerCallsign,
            IsEngineer = player.Account?.Qualifications?.Engineer ?? false,
            Rank = player.Rank
        };
    }

    private string ResolveObjectClass(InternalPlayer player, InternalUnit unit)
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

    private List<InternalPlayer> ResolveSlots(InternalUnit unit)
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
                    new InternalPlayer
                    {
                        DisplayName = settings.FillerName ?? "Reserve",
                        Unit = unit,
                        Rank = _ranks.Find(r => r.Name == (settings.FillerRank ?? "Recruit"))
                    }
                );
            }
        }

        return slots;
    }

    private void AggregateSpecialUnits(List<InternalUnit> orderedUnits)
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
        var positionPriorities = new[]
            {
                new { Key = chainOfCommand.First, Value = 3 },
                new { Key = chainOfCommand.Second, Value = 2 },
                new { Key = chainOfCommand.Third, Value = 1 },
                new { Key = chainOfCommand.Nco, Value = 0 }
            }.Where(x => x.Key != null)
             .ToDictionary(x => x.Key, x => x.Value);

        return positionPriorities.TryGetValue(playerId, out var priority) ? priority : -1;
    }

    private void SortSlots(List<InternalPlayer> slots, InternalUnit unit)
    {
        slots.Sort((a, b) =>
            {
                var priorityA = GetChainOfCommandSortPriority(a, unit);
                var priorityB = GetChainOfCommandSortPriority(b, unit);
                var rankA = _ranks.IndexOf(a.Rank);
                var rankB = _ranks.IndexOf(b.Rank);
                return priorityA < priorityB ? 1 :
                    priorityA > priorityB    ? -1 :
                    rankA < rankB            ? -1 :
                    rankA > rankB            ? 1 : string.CompareOrdinal(a.DisplayName, b.DisplayName);
            }
        );
    }

    private void InsertUnitChildren(List<InternalUnit> newUnits, InternalUnit parent)
    {
        var children = _units.Where(u => u.Source.Parent == parent.Source.Id).OrderBy(u => u.Source.Order).ToList();
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

    private class InternalUnit
    {
        public DomainUnit Source { get; init; }
        public List<InternalPlayer> Members { get; set; } = [];
        public Dictionary<string, InternalPlayer> Roles { get; } = new();
    }

    private class InternalPlayer
    {
        public DomainAccount Account { get; init; }
        public string DisplayName { get; init; }
        public DomainRank Rank { get; init; }
        public InternalUnit Unit { get; set; }
    }
}
