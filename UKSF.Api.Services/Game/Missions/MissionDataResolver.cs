using System.Collections.Generic;
using System.Linq;
using UKSF.Api.Models.Mission;

namespace UKSF.Api.Services.Game.Missions {
    public static class MissionDataResolver {
        // TODO: Add special display to variables area that resolves IDs as display names, unit names, ranks, roles, etc

        public static string ResolveObjectClass(MissionPlayer player) {
            if (IsMedic(player)) return "UKSF_B_Medic"; // Team Medic

            return player.unit.sourceUnit.id switch {
                "5a435eea905d47336442c75a" => "UKSF_B_Pilot", // "Joint Special Forces Aviation Wing"
                "5a848590eab14d12cc7fa618" => "UKSF_B_Pilot", // "RAF Cranwell"
                "5c98d7b396dba31f24cdb19c" => "UKSF_B_Pilot", // "51 Squadron"
                "5a441619730e9d162834500b" => "UKSF_B_Pilot_7", // "7 Squadron"
                "5a441602730e9d162834500a" => "UKSF_B_Pilot_656", // "656 Squadron"
                "5a4415d8730e9d1628345007" => "UKSF_B_Pilot_617", // "617 Squadron"
                "5a68b28e196530164c9b4fed" => "UKSF_B_Sniper", // "Sniper Platoon"
                "5b9123ca7a6c1f0e9875601c" => "UKSF_B_Medic", // "3 Medical Regiment"
                // "5a42835b55d6109bf0b081bd" => ResolvePlayerUnitRole(player) == 3 ? "UKSF_B_Officer" : "UKSF_B_Rifleman", // "UKSF"
                "5a42835b55d6109bf0b081bd" => "UKSF_B_Pilot", // "UKSF"
                _                          => ResolvePlayerUnitRole(player) != -1 ? "UKSF_B_SectionLeader" : "UKSF_B_Rifleman"
            };
        }

        private static int ResolvePlayerUnitRole(MissionPlayer player) {
            if (player.unit.roles.ContainsKey("1iC") && player.unit.roles["1iC"] == player) return 3;
            if (player.unit.roles.ContainsKey("2iC") && player.unit.roles["2iC"] == player) return 2;
            if (player.unit.roles.ContainsKey("3iC") && player.unit.roles["3iC"] == player) return 1;
            if (player.unit.roles.ContainsKey("NCOiC") && player.unit.roles["NCOiC"] == player) return 0;
            return -1;
        }

        private static bool IsMedic(MissionPlayer player) => MissionPatchData.instance.medicIds.Contains(player.account?.id);

        public static bool IsEngineer(MissionPlayer player) => MissionPatchData.instance.engineerIds.Contains(player.account?.id);

        public static string ResolveCallsign(MissionUnit unit, string defaultCallsign) {
            return unit.sourceUnit.id switch {
                "5a42835b55d6109bf0b081bd" => "JSFAW", // "UKSF"
                "5a435eea905d47336442c75a" => "JSFAW", // "Joint Special Forces Aviation Wing"
                "5a441619730e9d162834500b" => "JSFAW", // "7 Squadron"
                "5a441602730e9d162834500a" => "JSFAW", // "656 Squadron"
                "5a4415d8730e9d1628345007" => "JSFAW", // "617 Squadron"
                "5a848590eab14d12cc7fa618" => "JSFAW", // "RAF Cranwell"
                "5c98d7b396dba31f24cdb19c" => "JSFAW", // "51 Squadron"
                _                          => defaultCallsign
            };
        }

        public static void ResolveSpecialUnits(ref List<MissionUnit> orderedUnits) {
            List<MissionUnit> newOrderedUnits = new List<MissionUnit>();
            foreach (MissionUnit unit in orderedUnits) {
                switch (unit.sourceUnit.id) {
                    case "5a42835b55d6109bf0b081bd": // "UKSF"
                    case "5a441619730e9d162834500b": // "7 Squadron"
                    case "5a441602730e9d162834500a": // "656 Squadron"
                    case "5a4415d8730e9d1628345007": // "617 Squadron"
                    case "5a848590eab14d12cc7fa618": // "RAF Cranwell"
                    case "5c98d7b396dba31f24cdb19c": // "51 Squadron"
                        continue;
                    default:
                        newOrderedUnits.Add(unit);
                        break;
                }
            }

            orderedUnits = newOrderedUnits;
        }

        public static List<MissionPlayer> ResolveUnitSlots(MissionUnit unit) {
            List<MissionPlayer> slots = new List<MissionPlayer>();
            int max = 8;
            int fillerCount;
            switch (unit.sourceUnit.id) {
                case "5a435eea905d47336442c75a": // "Joint Special Forces Aviation Wing"
                    slots.AddRange(MissionPatchData.instance.units.Find(x => x.sourceUnit.id == "5a42835b55d6109bf0b081bd")?.members ?? new List<MissionPlayer>());
                    slots.AddRange(MissionPatchData.instance.units.Find(x => x.sourceUnit.id == "5a435eea905d47336442c75a")?.members ?? new List<MissionPlayer>());
                    slots.AddRange(MissionPatchData.instance.units.Find(x => x.sourceUnit.id == "5a441619730e9d162834500b")?.members ?? new List<MissionPlayer>());
                    slots.AddRange(MissionPatchData.instance.units.Find(x => x.sourceUnit.id == "5a441602730e9d162834500a")?.members ?? new List<MissionPlayer>());
                    slots.AddRange(MissionPatchData.instance.units.Find(x => x.sourceUnit.id == "5a4415d8730e9d1628345007")?.members ?? new List<MissionPlayer>());
                    slots.AddRange(MissionPatchData.instance.units.Find(x => x.sourceUnit.id == "5a848590eab14d12cc7fa618")?.members ?? new List<MissionPlayer>());
                    slots.AddRange(MissionPatchData.instance.units.Find(x => x.sourceUnit.id == "5c98d7b396dba31f24cdb19c")?.members ?? new List<MissionPlayer>());
                    break;
                case "5a68b28e196530164c9b4fed": // "Sniper Platoon"
                    max = 3;
                    slots.AddRange(unit.members);
                    fillerCount = max - slots.Count;
                    for (int i = 0; i < fillerCount; i++) {
                        MissionPlayer player = new MissionPlayer { name = "Sniper", unit = unit, rank = MissionPatchData.instance.ranks.Find(x => x.name == "Private") };
                        player.objectClass = ResolveObjectClass(player);
                        slots.Add(player);
                    }

                    break;
                case "5bbbb9645eb3a4170c488b36": // "Guardian 1-1"
                case "5bbbbdab5eb3a4170c488f2e": // "Guardian 1-2"
                case "5bbbbe365eb3a4170c488f30": // "Guardian 1-3"
                    slots.AddRange(unit.members);
                    fillerCount = max - slots.Count;
                    for (int i = 0; i < fillerCount; i++) {
                        MissionPlayer player = new MissionPlayer { name = "Reserve", unit = unit, rank = MissionPatchData.instance.ranks.Find(x => x.name == "Recruit") };
                        player.objectClass = ResolveObjectClass(player);
                        slots.Add(player);
                    }

                    break;
                case "5ad748e0de5d414f4c4055e0": // "Guardian 1-R"
                    for (int i = 0; i < 6; i++) {
                        MissionPlayer player = new MissionPlayer { name = "Reserve", unit = unit, rank = MissionPatchData.instance.ranks.Find(x => x.name == "Recruit") };
                        player.objectClass = ResolveObjectClass(player);
                        slots.Add(player);
                    }

                    break;
                default:
                    slots = unit.members;
                    break;
            }

            slots.Sort(
                (a, b) => {
                    int roleA = ResolvePlayerUnitRole(a);
                    int roleB = ResolvePlayerUnitRole(b);
                    int rankA = MissionPatchData.instance.ranks.IndexOf(a.rank);
                    int rankB = MissionPatchData.instance.ranks.IndexOf(b.rank);
                    return roleA < roleB ? 1 : roleA > roleB ? -1 : rankA < rankB ? -1 : rankA > rankB ? 1 : string.CompareOrdinal(a.name, b.name);
                }
            );
            return slots;
        }

        public static bool IsUnitPermanent(MissionUnit unit) {
            return unit.sourceUnit.id switch {
                "5bbbb9645eb3a4170c488b36" => true, // "Guardian 1-1"
                "5bbbbdab5eb3a4170c488f2e" => true, // "Guardian 1-2"
                "5bbbbe365eb3a4170c488f30" => true, // "Guardian 1-3"
                "5ad748e0de5d414f4c4055e0" => true, // "Guardian 1-R"
                _                          => false
            };
        }
    }
}
