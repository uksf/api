using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UKSFWebsite.Api.Models.Mission;
using UKSFWebsite.Api.Services.Utility;

namespace UKSFWebsite.Api.Services.Game.Missions {
    public class MissionService {
        private const string UNBIN = "C:\\Program Files (x86)\\Mikero\\DePboTools\\bin\\DeRapDos.exe";

        private readonly MissionPatchDataService missionPatchDataService;

        private Mission mission;
        private List<MissionPatchingReport> reports;

        public MissionService(MissionPatchDataService missionPatchDataService) => this.missionPatchDataService = missionPatchDataService;

        public List<MissionPatchingReport> ProcessMission(Mission tempMission) {
            mission = tempMission;
            reports = new List<MissionPatchingReport>();
            if (!AssertRequiredFiles()) return reports;

            if (CheckBinned()) {
                UnBin();
            }

            Read();

            if (CheckIgnoreKey("missionPatchingIgnore")) {
                reports.Add(
                    new MissionPatchingReport(
                        "Mission Patching Ignored",
                        "Mission patching for this mission was ignored.\nThis means no changes to the mission.sqm were made. This is not an error, however errors may occur in the mission as a result of this.\nEnsure ALL the steps below have been done to the mission.sqm before reporting any errors:\n\n\n1: Remove raw newline characters. Any newline characters (\\n) in code will result in compile errors and that code will NOT run.\nFor example, a line: init = \"myTestVariable = 10; \\n myOtherTestVariable = 20;\" should be replaced with: init = \"myTestVariable = 10; myOtherTestVariable = 20;\"\n\n2: Replace embedded quotes. Any embedded quotes (\"\") in code will result in compile errors and that code will NOT run. They should be replaced with a single quote character (').\nFor example, a line: init = \"myTestVariable = \"\"hello\"\";\" should be replaced with: init = \"myTestVariable = 'hello';\""
                    )
                );
                PatchDescription();
                return reports;
            }

            missionPatchDataService.UpdatePatchData();
            Patch();
            Write();
            PatchDescription();
            return reports;
        }

        private bool AssertRequiredFiles() {
            if (!File.Exists(mission.descriptionPath)) {
                reports.Add(new MissionPatchingReport("Missing file: description.ext", "The mission is missing a required file:\ndescription.ext\n\nIt is advised to copy this file directly from the template mission to your mission\nUKSFTemplate.VR is located in the modpack files", true));
                return false;
            }

            if (!File.Exists(Path.Combine(mission.path, "cba_settings.sqf"))) {
                reports.Add(new MissionPatchingReport("Missing file: cba_settings.sqf", "The mission is missing a required file:\ncba_settings.sqf\n\nIt is advised to copy this file directly from the template mission to your mission\nUKSFTemplate.VR is located in the modpack files", true));
                return false;
            }

            if (File.Exists(Path.Combine(mission.path, "README.txt"))) {
                File.Delete(Path.Combine(mission.path, "README.txt"));
            }

            return true;
        }

        private bool CheckIgnoreKey(string key) {
            mission.descriptionLines = File.ReadAllLines(mission.descriptionPath).ToList();
            return mission.descriptionLines.Any(x => x.ContainsCaseInsensitive(key));
        }

        private bool CheckBinned() {
            Process process = new Process {StartInfo = {FileName = UNBIN, Arguments = $"-p -q \"{mission.sqmPath}\"", UseShellExecute = false, CreateNoWindow = true}};
            process.Start();
            process.WaitForExit();
            return process.ExitCode == 0;
        }

        private void UnBin() {
            Process process = new Process {StartInfo = {FileName = UNBIN, Arguments = $"-p \"{mission.sqmPath}\"", UseShellExecute = false, CreateNoWindow = true}};
            process.Start();
            process.WaitForExit();

            if (File.Exists($"{mission.sqmPath}.txt")) {
                File.Delete(mission.sqmPath);
                File.Move($"{mission.sqmPath}.txt", mission.sqmPath);
            } else {
                throw new FileNotFoundException();
            }
        }

        private void Read() {
            mission.sqmLines = File.ReadAllLines(mission.sqmPath).Select(x => x.Trim()).ToList();
            mission.sqmLines.RemoveAll(string.IsNullOrEmpty);
            RemoveUnbinText();
            ReadAllData();
        }

        private void RemoveUnbinText() {
            if (mission.sqmLines.First() != "////////////////////////////////////////////////////////////////////") return;
            mission.sqmLines = mission.sqmLines.Skip(7).ToList();
            mission.sqmLines = mission.sqmLines.Take(mission.sqmLines.Count - 1).ToList();
        }

        private void ReadAllData() {
            Mission.nextId = Convert.ToInt32(MissionUtilities.ReadSingleDataByKey(MissionUtilities.ReadDataByKey(mission.sqmLines, "ItemIDProvider"), "nextID"));
            mission.rawEntities = MissionUtilities.ReadDataByKey(mission.sqmLines, "Entities");
            mission.missionEntity = MissionEntityHelper.CreateFromItems(mission.rawEntities);
        }

        private void Patch() {
            mission.missionEntity.Patch();
            if (!CheckIgnoreKey("missionImageIgnore")) {
                string imagePath = Path.Combine(mission.path, "uksf.paa");
                string modpackImagePath = Path.Combine(VariablesWrapper.VariablesDataService().GetSingle("PATH_MODPACK").AsString(), "@uksf", "UKSFTemplate.VR", "uksf.paa");
                if (File.Exists(modpackImagePath)) {
                    if (File.Exists(imagePath) && new FileInfo(imagePath).Length != new FileInfo(modpackImagePath).Length) {
                        reports.Add(
                            new MissionPatchingReport(
                                "Loading image was different",
                                "The mission loading image `uksf.paa` was different from the default and has been replaced\n\nIf you wish this to be a custom image, see <a target=\"_blank\" href=https://github.com/uksf/modpack/wiki/SR5:-Mission-Patching#ignoring-custom-loading-image>this page</a> for details on how to achieve this"
                            )
                        );
                    }

                    File.Copy(modpackImagePath, imagePath, true);
                }
            }
        }

        private void Write() {
            int start = MissionUtilities.GetIndexByKey(mission.sqmLines, "Entities");
            int count = mission.rawEntities.Count;
            mission.sqmLines.RemoveRange(start, count);
            IEnumerable<string> newEntities = mission.missionEntity.Serialize();
            mission.sqmLines.InsertRange(start, newEntities);
            mission.sqmLines = mission.sqmLines.Select(x => x.RemoveNewLines().RemoveEmbeddedQuotes()).ToList();
            File.WriteAllLines(mission.sqmPath, mission.sqmLines);
        }

        private void PatchDescription() {
            int playable = mission.sqmLines.Select(x => x.RemoveSpaces()).Count(x => x.ContainsCaseInsensitive("isPlayable=1") || x.ContainsCaseInsensitive("isPlayer=1"));
            mission.playerCount = playable;

            mission.descriptionLines = File.ReadAllLines(mission.descriptionPath).ToList();
            mission.descriptionLines[mission.descriptionLines.FindIndex(x => x.ContainsCaseInsensitive("maxPlayers"))] = $"    maxPlayers = {playable};";
            CheckDescriptionRequireds();
            CheckDescriptionConfigurables();

            int index = mission.descriptionLines.FindIndex(x => x.Contains("__EXEC"));
            if (index != -1) {
                mission.descriptionLines.RemoveAt(index);
            }

            File.WriteAllLines(mission.descriptionPath, mission.descriptionLines);
        }

        private void CheckDescriptionConfigurables() {
            CheckDescriptionItem("onLoadName", "\"UKSF: Operation\"", false);
            CheckDescriptionItem("onLoadMission", "\"UKSF: Operation\"", false);
            CheckDescriptionItem("overviewText", "\"UKSF: Operation\"", false);
        }

        private void CheckDescriptionRequireds() {
            CheckDescriptionItem("author", "\"UKSF\"");
            CheckDescriptionItem("loadScreen", "\"uksf.paa\"");
            CheckDescriptionItem("respawn", "\"BASE\"");
            CheckDescriptionItem("respawnOnStart", "1");
            CheckDescriptionItem("respawnDelay", "1");
            CheckDescriptionItem("respawnDialog", "0");
            CheckDescriptionItem("respawnTemplates[]", "{ \"MenuPosition\" }");
            CheckDescriptionItem("reviveMode", "0");
            CheckDescriptionItem("disabledAI", "1");
            CheckDescriptionItem("aiKills", "0");
            CheckDescriptionItem("disableChannels[]", "{ 0,2,6 }");
            CheckDescriptionItem("cba_settings_hasSettingsFile", "1");
        }

        private void CheckDescriptionItem(string key, string defaultValue, bool required = true) {
            int index = mission.descriptionLines.FindIndex(x => x.Contains($"{key} = ") || x.Contains($"{key}=") || x.Contains($"{key}= ") || x.Contains($"{key} ="));
            if (index != -1) {
                string itemValue = mission.descriptionLines[index].Split("=")[1].Trim();
                itemValue = itemValue.Remove(itemValue.Length - 1);
                bool equal = string.Equals(itemValue, defaultValue, StringComparison.InvariantCultureIgnoreCase);
                if (!equal && required) {
                    reports.Add(new MissionPatchingReport($"Required description.ext item <i>{key}</i>  value is not default", $"<i>{key}</i>  in description.ext is '{itemValue}'\nThe default value is '{defaultValue}'\n\nYou should only change this if you know what you're doing"));
                } else if (equal && !required) {
                    reports.Add(new MissionPatchingReport($"Configurable description.ext item <i>{key}</i>  value is default", $"<i>{key}</i>  in description.ext is the same as the default value '{itemValue}'\n\nThis should be changed based on your mission"));
                }

                return;
            }

            if (required) {
                mission.descriptionLines.Add($"{key} = {defaultValue};");
            } else {
                reports.Add(
                    new MissionPatchingReport(
                        $"Configurable description.ext item <i>{key}</i>  is missing",
                        $"<i>{key}</i>  in description.ext is missing\nThis is required for the mission\n\nIt is advised to copy the description.ext file directly from the template mission to your mission\nUKSFTemplate.VR is located in the modpack files",
                        true
                    )
                );
            }
        }
    }
}