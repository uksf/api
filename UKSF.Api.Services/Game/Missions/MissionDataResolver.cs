using System.Collections.Generic;
using System.Linq;
using UKSF.Api.Models.Mission;

namespace UKSF.Api.Services.Game.Missions {
    public class MissionDataResolver {
        private static readonly string[] ENGINEER_IDS = {
            "5a1e894463d0f71710089106", // Bridg
            "5a4e7effd68b7e16e46fc614", // Woodward
            "5a2439443fccaa15902aaa4e", // Mac
            "5a1a0ad55d0a76133837eb78", // Pot
            "5a4518559220c31b50966811", // Clarke
            "59e38f13594c603b78aa9dbf", // Carr
            "59e38f1b594c603b78aa9dc1", // Lars
            "5a1a14b5aacf7b00346dcc37" // Gilbert
        };

        public static string ResolveObjectClass(MissionPlayer player) {
            if (player.account?.id == "5b4b568c20e1fd00013752d1") return "UKSF_B_Medic"; // Smith, SAS Medic
            return player.unit.sourceUnit.id switch {
                "5a435eea905d47336442c75a" => // "Joint Special Forces Aviation Wing"
                "UKSF_B_Pilot",
                "5a848590eab14d12cc7fa618" => // "RAF Cranwell"
                "UKSF_B_Pilot",
                "5c98d7b396dba31f24cdb19c" => // "51 Squadron"
                "UKSF_B_Pilot",
                "5a441619730e9d162834500b" => // "7 Squadron"
                "UKSF_B_Pilot_7",
                "5a441602730e9d162834500a" => // "656 Squadron"
                "UKSF_B_Pilot_656",
                "5a4415d8730e9d1628345007" => // "617 Squadron"
                "UKSF_B_Pilot_617",
                "5a68b28e196530164c9b4fed" => // "Sniper Platoon"
                "UKSF_B_Sniper",
                "5a68c047196530164c9b4fee" => // "The Pathfinder Platoon"
                "UKSF_B_Pathfinder",
                "5bbbb8875eb3a4170c488b24" => // "Air Troop"
                "UKSF_B_SAS",
                "5ba8983ee12a331f94cb02d4" => // "SAS"
                "UKSF_B_Officer",
                "5b9123ca7a6c1f0e9875601c" => // "3 Medical Regiment"
                "UKSF_B_Medic",
                "5a42835b55d6109bf0b081bd" => // "UKSF"
                (ResolvePlayerUnitRole(player) == 3 ? "UKSF_B_Officer" : "UKSF_B_Rifleman"),
                _ => (ResolvePlayerUnitRole(player) != -1 ? "UKSF_B_SectionLeader" : "UKSF_B_Rifleman")
            };
        }

        private static int ResolvePlayerUnitRole(MissionPlayer player) {
            if (player.unit.roles.ContainsKey("1iC") && player.unit.roles["1iC"] == player) return 3;
            if (player.unit.roles.ContainsKey("2iC") && player.unit.roles["2iC"] == player) return 2;
            if (player.unit.roles.ContainsKey("3iC") && player.unit.roles["3iC"] == player) return 1;
            if (player.unit.roles.ContainsKey("NCOiC") && player.unit.roles["NCOiC"] == player) return 0;
            return -1;
        }

        public static bool IsEngineer(MissionPlayer player) => ENGINEER_IDS.Contains(player.account?.id);

        public static string ResolveCallsign(MissionUnit unit, string defaultCallsign) {
            return unit.sourceUnit.id switch {
                "5a435eea905d47336442c75a" => // "Joint Special Forces Aviation Wing"
                "JSFAW",
                "5a441619730e9d162834500b" => // "7 Squadron"
                "JSFAW",
                "5a441602730e9d162834500a" => // "656 Squadron"
                "JSFAW",
                "5a4415d8730e9d1628345007" => // "617 Squadron"
                "JSFAW",
                "5a848590eab14d12cc7fa618" => // "RAF Cranwell"
                "JSFAW",
                "5c98d7b396dba31f24cdb19c" => // "51 Squadron"
                "JSFAW",
                _ => defaultCallsign
            };
        }

        public static void ResolveSpecialUnits(ref List<MissionUnit> orderedUnits) {
            List<MissionUnit> newOrderedUnits = new List<MissionUnit>();
            foreach (MissionUnit unit in orderedUnits) {
                switch (unit.sourceUnit.id) {
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
                    slots.AddRange(MissionPatchData.instance.units.Find(x => x.sourceUnit.id == "5a435eea905d47336442c75a").members);
                    slots.AddRange(MissionPatchData.instance.units.Find(x => x.sourceUnit.id == "5a441619730e9d162834500b").members);
                    slots.AddRange(MissionPatchData.instance.units.Find(x => x.sourceUnit.id == "5a441602730e9d162834500a").members);
                    slots.AddRange(MissionPatchData.instance.units.Find(x => x.sourceUnit.id == "5a4415d8730e9d1628345007").members);
                    slots.AddRange(MissionPatchData.instance.units.Find(x => x.sourceUnit.id == "5a848590eab14d12cc7fa618").members);
                    slots.AddRange(MissionPatchData.instance.units.Find(x => x.sourceUnit.id == "5c98d7b396dba31f24cdb19c").members);
                    break;
                case "5a68b28e196530164c9b4fed": // "Sniper Platoon"
                    max = 3;
                    slots.AddRange(unit.members);
                    fillerCount = max - slots.Count;
                    for (int i = 0; i < fillerCount; i++) {
                        MissionPlayer player = new MissionPlayer {name = "Sniper", unit = unit, rank = MissionPatchData.instance.ranks.Find(x => x.name == "Private")};
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
                        MissionPlayer player = new MissionPlayer {name = "Reserve", unit = unit, rank = MissionPatchData.instance.ranks.Find(x => x.name == "Recruit")};
                        player.objectClass = ResolveObjectClass(player);
                        slots.Add(player);
                    }

                    break;
                case "5ad748e0de5d414f4c4055e0": // "Guardian 1-R"
                    for (int i = 0; i < 6; i++) {
                        MissionPlayer player = new MissionPlayer {name = "Reserve", unit = unit, rank = MissionPatchData.instance.ranks.Find(x => x.name == "Recruit")};
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
                    int unitDepthA = a.unit.depth;
                    int unitDepthB = b.unit.depth;
                    int unitOrderA = a.unit.sourceUnit.order;
                    int unitOrderB = b.unit.sourceUnit.order;
                    int rankA = MissionPatchData.instance.ranks.IndexOf(a.rank);
                    int rankB = MissionPatchData.instance.ranks.IndexOf(b.rank);
                    return unitDepthA < unitDepthB ? -1 : unitDepthA > unitDepthB ? 1 : unitOrderA < unitOrderB ? -1 : unitOrderA > unitOrderB ? 1 : roleA < roleB ? 1 : roleA > roleB ? -1 : rankA < rankB ? -1 : rankA > rankB ? 1 : string.CompareOrdinal(a.name, b.name);
                }
            );
            return slots;
        }

        public static bool IsUnitPermanent(MissionUnit unit) {
            return unit.sourceUnit.id switch {
                "5bbbb9645eb3a4170c488b36" => // "Guardian 1-1"
                true,
                "5bbbbdab5eb3a4170c488f2e" => // "Guardian 1-2"
                true,
                "5bbbbe365eb3a4170c488f30" => // "Guardian 1-3"
                true,
                "5ad748e0de5d414f4c4055e0" => // "Guardian 1-R"
                true,
                _ => false
            };
        }
    }
}
