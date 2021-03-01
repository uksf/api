using System.Collections.Generic;
using System.Linq;
using UKSF.Api.ArmaMissions.Models;

namespace UKSF.Api.ArmaMissions.Services {
    public static class MissionDataResolver {
        // TODO: Add special display to variables area that resolves IDs as display names, unit names, ranks, roles, etc

        public static string ResolveObjectClass(MissionPlayer player) {
            if (IsMedic(player)) return "UKSF_B_Medic"; // Team Medic

            return player.Unit.SourceUnit.Id switch {
                "5a435eea905d47336442c75a" => "UKSF_B_Pilot", // "Joint Special Forces Aviation Wing"
                "5fe39de7815f5f03801134f7" => "UKSF_B_Pilot", // "Combat Ready"
                "5a848590eab14d12cc7fa618" => "UKSF_B_Pilot", // "RAF Cranwell"
                "5a68b28e196530164c9b4fed" => "UKSF_B_Sniper", // "Sniper Platoon"
                "5b9123ca7a6c1f0e9875601c" => "UKSF_B_Medic", // "3 Medical Regiment"
                // "5a42835b55d6109bf0b081bd" => ResolvePlayerUnitRole(player) == 3 ? "UKSF_B_Officer" : "UKSF_B_Rifleman", // "UKSF"
                "5a42835b55d6109bf0b081bd" => "UKSF_B_Pilot", // "UKSF"
                _                          => ResolvePlayerUnitRole(player) != -1 ? "UKSF_B_SectionLeader" : "UKSF_B_Rifleman"
            };
        }

        private static int ResolvePlayerUnitRole(MissionPlayer player) {
            if (player.Unit.Roles.ContainsKey("1iC") && player.Unit.Roles["1iC"] == player) return 3;
            if (player.Unit.Roles.ContainsKey("2iC") && player.Unit.Roles["2iC"] == player) return 2;
            if (player.Unit.Roles.ContainsKey("3iC") && player.Unit.Roles["3iC"] == player) return 1;
            if (player.Unit.Roles.ContainsKey("NCOiC") && player.Unit.Roles["NCOiC"] == player) return 0;
            return -1;
        }

        private static bool IsMedic(MissionPlayer player) => MissionPatchData.Instance.MedicIds.Contains(player.Account?.Id);

        public static bool IsEngineer(MissionPlayer player) => MissionPatchData.Instance.EngineerIds.Contains(player.Account?.Id);

        public static string ResolveCallsign(MissionUnit unit, string defaultCallsign) {
            return unit.SourceUnit.Id switch {
                "5a42835b55d6109bf0b081bd" => "JSFAW", // "UKSF"
                "5a435eea905d47336442c75a" => "JSFAW", // "Joint Special Forces Aviation Wing"
                "5fe39de7815f5f03801134f7" => "JSFAW", // "Combat Ready"
                "5a848590eab14d12cc7fa618" => "JSFAW", // "RAF Cranwell"
                _                          => defaultCallsign
            };
        }

        public static void ResolveSpecialUnits(List<MissionUnit> orderedUnits) {
            List<string> ids = new() {
                "5a42835b55d6109bf0b081bd", // "UKSF"
                "5fe39de7815f5f03801134f7", // "Combat Ready"
                "5a848590eab14d12cc7fa618" // "RAF Cranwell"
            };
            orderedUnits.RemoveAll(x => ids.Contains(x.SourceUnit.Id));
        }

        public static List<MissionPlayer> ResolveUnitSlots(MissionUnit unit) {
            List<MissionPlayer> slots = new();
            int max = 12;
            int fillerCount;
            switch (unit.SourceUnit.Id) {
                case "5a435eea905d47336442c75a": // "Joint Special Forces Aviation Wing"
                    slots.AddRange(MissionPatchData.Instance.Units.Find(x => x.SourceUnit.Id == "5a42835b55d6109bf0b081bd")?.Members ?? new List<MissionPlayer>());
                    slots.AddRange(MissionPatchData.Instance.Units.Find(x => x.SourceUnit.Id == "5a435eea905d47336442c75a")?.Members ?? new List<MissionPlayer>());
                    slots.AddRange(MissionPatchData.Instance.Units.Find(x => x.SourceUnit.Id == "5fe39de7815f5f03801134f7")?.Members ?? new List<MissionPlayer>());
                    slots.AddRange(MissionPatchData.Instance.Units.Find(x => x.SourceUnit.Id == "5a848590eab14d12cc7fa618")?.Members ?? new List<MissionPlayer>());
                    break;
                case "5a68b28e196530164c9b4fed": // "Sniper Platoon"
                    max = 3;
                    slots.AddRange(unit.Members);
                    fillerCount = max - slots.Count;
                    for (int i = 0; i < fillerCount; i++) {
                        MissionPlayer player = new() { Name = "Sniper", Unit = unit, Rank = MissionPatchData.Instance.Ranks.Find(x => x.Name == "Private") };
                        player.ObjectClass = ResolveObjectClass(player);
                        slots.Add(player);
                    }

                    break;
                case "5bbbb9645eb3a4170c488b36": // "Guardian 1-1"
                case "5bbbbdab5eb3a4170c488f2e": // "Guardian 1-2"
                case "5bbbbe365eb3a4170c488f30": // "Guardian 1-3"
                    slots.AddRange(unit.Members);
                    fillerCount = max - slots.Count;
                    for (int i = 0; i < fillerCount; i++) {
                        MissionPlayer player = new() { Name = "Reserve", Unit = unit, Rank = MissionPatchData.Instance.Ranks.Find(x => x.Name == "Recruit") };
                        player.ObjectClass = ResolveObjectClass(player);
                        slots.Add(player);
                    }

                    break;
                case "5ad748e0de5d414f4c4055e0": // "Guardian 1-R"
                    for (int i = 0; i < 10; i++) {
                        MissionPlayer player = new() { Name = "Reserve", Unit = unit, Rank = MissionPatchData.Instance.Ranks.Find(x => x.Name == "Recruit") };
                        player.ObjectClass = ResolveObjectClass(player);
                        slots.Add(player);
                    }

                    break;
                default:
                    slots = unit.Members;
                    break;
            }

            slots.Sort(
                (a, b) => {
                    int roleA = ResolvePlayerUnitRole(a);
                    int roleB = ResolvePlayerUnitRole(b);
                    int rankA = MissionPatchData.Instance.Ranks.IndexOf(a.Rank);
                    int rankB = MissionPatchData.Instance.Ranks.IndexOf(b.Rank);
                    return roleA < roleB ? 1 : roleA > roleB ? -1 : rankA < rankB ? -1 : rankA > rankB ? 1 : string.CompareOrdinal(a.Name, b.Name);
                }
            );
            return slots;
        }

        public static bool IsUnitPermanent(MissionUnit unit) {
            return unit.SourceUnit.Id switch {
                "5bbbb9645eb3a4170c488b36" => true, // "Guardian 1-1"
                "5bbbbdab5eb3a4170c488f2e" => true, // "Guardian 1-2"
                "5bbbbe365eb3a4170c488f30" => true, // "Guardian 1-3"
                "5ad748e0de5d414f4c4055e0" => true, // "Guardian 1-R"
                _                          => false
            };
        }
    }
}
