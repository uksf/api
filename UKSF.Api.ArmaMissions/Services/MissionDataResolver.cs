using UKSF.Api.ArmaMissions.Models;

namespace UKSF.Api.ArmaMissions.Services;

public static class MissionDataResolver
{
    // TODO: Add special display to variables area that resolves IDs as display names, unit names, ranks, roles, etc

    public static string ResolveObjectClass(MissionPlayer player)
    {
        if (IsPilot(player.Unit.SourceUnit.Id))
        {
            return "UKSF_B_Pilot";
        }

        if (IsMedic(player))
        {
            return "UKSF_B_Medic"; // Team Medic
        }

        return player.Unit.SourceUnit.Id switch
        {
            "5a68b28e196530164c9b4fed" => "UKSF_B_Sniper", // "Sniper Platoon"
            "5b9123ca7a6c1f0e9875601c" => "UKSF_B_Medic", // "3 Medical Regiment"
            // "5a42835b55d6109bf0b081bd" => GetChainOfCommandSortPriority(player) == 3 ? "UKSF_B_Officer" : "UKSF_B_Rifleman", // "UKSF"
            _ => GetChainOfCommandSortPriority(player) != -1 ? "UKSF_B_SectionLeader" : "UKSF_B_Rifleman"
        };
    }

    private static int GetChainOfCommandSortPriority(MissionPlayer player)
    {
        var chainOfCommand = player.Unit.SourceUnit.ChainOfCommand;
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

    private static bool IsPilot(string id)
    {
        return id is // "5a42835b55d6109bf0b081bd" // "UKSF"
            "5a435eea905d47336442c75a" // "Joint Special Forces Aviation Wing"
            or "5fe39de7815f5f03801134f7" // "Combat Ready"
            or "5a848590eab14d12cc7fa618"; // "RAF Cranwell"
    }

    private static bool IsMedic(MissionPlayer player)
    {
        return player.Account?.Qualifications?.Medic ?? false;
    }

    public static bool IsEngineer(MissionPlayer player)
    {
        return player.Account?.Qualifications?.Engineer ?? false;
    }

    public static string ResolveCallsign(MissionUnit unit, string defaultCallsign)
    {
        return IsPilot(unit.SourceUnit.Id) ? "JSFAW" : defaultCallsign;
    }

    public static void ResolveSpecialUnits(List<MissionUnit> orderedUnits)
    {
        List<string> ids =
        [
            "5fe39de7815f5f03801134f7", // "Combat Ready"
            "5a848590eab14d12cc7fa618" // "RAF Cranwell"
        ];
        orderedUnits.RemoveAll(x => ids.Contains(x.SourceUnit.Id));
    }

    public static List<MissionPlayer> ResolveUnitSlots(MissionUnit unit)
    {
        List<MissionPlayer> slots = [];
        var max = 12;
        int fillerCount;
        switch (unit.SourceUnit.Id)
        {
            case "5a435eea905d47336442c75a": // "Joint Special Forces Aviation Wing"
                // slots.AddRange(MissionPatchData.Instance.Units.Find(x => x.SourceUnit.Id == "5a42835b55d6109bf0b081bd")?.Members ?? new List<MissionPlayer>());
                slots.AddRange(MissionPatchData.Instance.Units.Find(x => x.SourceUnit.Id == "5a435eea905d47336442c75a")?.Members ?? []);
                slots.AddRange(MissionPatchData.Instance.Units.Find(x => x.SourceUnit.Id == "5fe39de7815f5f03801134f7")?.Members ?? []);
                slots.AddRange(MissionPatchData.Instance.Units.Find(x => x.SourceUnit.Id == "5a848590eab14d12cc7fa618")?.Members ?? []);
                break;
            case "5a68b28e196530164c9b4fed": // "Sniper Platoon"
                max = 3;
                slots.AddRange(unit.Members);
                fillerCount = max - slots.Count;
                for (var i = 0; i < fillerCount; i++)
                {
                    MissionPlayer player = new()
                    {
                        Name = "Sniper",
                        Unit = unit,
                        Rank = MissionPatchData.Instance.Ranks.Find(x => x.Name == "Private")
                    };
                    player.ObjectClass = ResolveObjectClass(player);
                    slots.Add(player);
                }

                break;
            case "5bbbb9645eb3a4170c488b36": // "Kestrel"
            case "5bbbbdab5eb3a4170c488f2e": // "Raider"
            case "5bbbbe365eb3a4170c488f30": // "Claymore"
                slots.AddRange(unit.Members);
                fillerCount = max - slots.Count;
                for (var i = 0; i < fillerCount; i++)
                {
                    MissionPlayer player = new()
                    {
                        Name = "Reserve",
                        Unit = unit,
                        Rank = MissionPatchData.Instance.Ranks.Find(x => x.Name == "Recruit")
                    };
                    player.ObjectClass = ResolveObjectClass(player);
                    slots.Add(player);
                }

                break;
            case "5ad748e0de5d414f4c4055e0": // "Reserves"
                max = 10;
                slots.AddRange(unit.Members);
                fillerCount = max - slots.Count;
                for (var i = 0; i < fillerCount; i++)
                {
                    MissionPlayer player = new()
                    {
                        Name = "Reserve",
                        Unit = unit,
                        Rank = MissionPatchData.Instance.Ranks.Find(x => x.Name == "Recruit")
                    };
                    player.ObjectClass = ResolveObjectClass(player);
                    slots.Add(player);
                }

                break;
            default: slots = unit.Members; break;
        }

        slots.Sort((a, b) =>
            {
                var priorityA = GetChainOfCommandSortPriority(a);
                var priorityB = GetChainOfCommandSortPriority(b);
                var rankA = MissionPatchData.Instance.Ranks.IndexOf(a.Rank);
                var rankB = MissionPatchData.Instance.Ranks.IndexOf(b.Rank);
                return priorityA < priorityB ? 1 :
                    priorityA > priorityB    ? -1 :
                    rankA < rankB            ? -1 :
                    rankA > rankB            ? 1 : string.CompareOrdinal(a.Name, b.Name);
            }
        );
        return slots;
    }

    public static bool IsUnitPermanent(MissionUnit unit)
    {
        // "Guardian 1-1", "Guardian 1-2", "Guardian 1-3", "Guardian 1-R"
        return unit.SourceUnit.Id is "5bbbb9645eb3a4170c488b36" or "5bbbbdab5eb3a4170c488f2e" or "5bbbbe365eb3a4170c488f30" or "5ad748e0de5d414f4c4055e0";
    }
}
