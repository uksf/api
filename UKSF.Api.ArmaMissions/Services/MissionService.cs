using System.Diagnostics;
using UKSF.Api.ArmaMissions.Models;
using UKSF.Api.Core.Extensions;
using UKSF.Api.Core.Models;

namespace UKSF.Api.ArmaMissions.Services;

public class MissionService(MissionPatchDataService missionPatchDataService)
{
    private const string Unbin = "C:\\Program Files (x86)\\Mikero\\DePboTools\\bin\\DeRapDos.exe";

    private int _armaServerDefaultMaxCurators;
    private string _armaServerModsPath;
    private Mission _mission;
    private List<ValidationReport> _reports;

    public List<ValidationReport> ProcessMission(Mission tempMission, string armaServerModsPath, int armaServerDefaultMaxCurators)
    {
        _armaServerDefaultMaxCurators = armaServerDefaultMaxCurators;
        _armaServerModsPath = armaServerModsPath;
        _mission = tempMission;
        _reports = new List<ValidationReport>();

        if (!AssertRequiredFiles())
        {
            return _reports;
        }

        if (CheckBinned())
        {
            UnBin();
        }

        Read();

        if (MissionUtilities.CheckFlag(_mission, "missionPatchingIgnore"))
        {
            _reports.Add(
                new ValidationReport(
                    "Mission Patching Ignored",
                    "Mission patching for this mission was ignored.\nThis means no changes to the mission.sqm were made." +
                    "This is not an error, however errors may occur in the mission as a result of this.\n" +
                    "Ensure ALL the steps below have been done to the mission.sqm before reporting any errors:\n\n\n" +
                    "1: Remove raw newline characters. Any newline characters (\\n) in code will result in compile errors and that code will NOT run.\n" +
                    "For example, a line: init = \"myTestVariable = 10; \\n myOtherTestVariable = 20;\" should be replaced with: init = \"myTestVariable = 10; myOtherTestVariable = 20;\"\n\n" +
                    "2: Replace embedded quotes. Any embedded (double) quotes (\"\"hello\"\") in code will result in compile errors and that code will NOT run. They should be replaced with a single quote character (').\n" +
                    "For example, a line: init = \"myTestVariable = \"\"hello\"\";\" should be replaced with: init = \"myTestVariable = 'hello';\""
                )
            );
            PatchDescription();
            return _reports;
        }

        missionPatchDataService.UpdatePatchData();
        Patch();
        Write();
        PatchDescription();
        return _reports;
    }

    private bool AssertRequiredFiles()
    {
        if (!File.Exists(_mission.DescriptionPath))
        {
            _reports.Add(
                new ValidationReport(
                    "Missing file: description.ext",
                    "The mission is missing a required file:\ndescription.ext\n\n" +
                    "It is advised to copy this file directly from the template mission to your mission\nUKSFTemplate.VR is located in the modpack files",
                    true
                )
            );
            return false;
        }

        if (!File.Exists(Path.Combine(_mission.Path, "cba_settings.sqf")))
        {
            _reports.Add(
                new ValidationReport(
                    "Missing file: cba_settings.sqf",
                    "The mission is missing a required file:\ncba_settings.sqf\n\n" +
                    "It is advised to copy this file directly from the template mission to your mission and make changes according to the needs of the mission\n" +
                    "UKSFTemplate.VR is located in the modpack files",
                    true
                )
            );
            return false;
        }

        if (File.Exists(Path.Combine(_mission.Path, "README.txt")))
        {
            File.Delete(Path.Combine(_mission.Path, "README.txt"));
        }

        return true;
    }

    private bool CheckBinned()
    {
        Process process = new()
        {
            StartInfo =
            {
                FileName = Unbin,
                Arguments = $"-p -q \"{_mission.SqmPath}\"",
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };
        process.Start();
        process.WaitForExit();
        return process.ExitCode == 0;
    }

    private void UnBin()
    {
        Process process = new()
        {
            StartInfo =
            {
                FileName = Unbin,
                Arguments = $"-p \"{_mission.SqmPath}\"",
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };
        process.Start();
        process.WaitForExit();

        if (File.Exists($"{_mission.SqmPath}.txt"))
        {
            File.Delete(_mission.SqmPath);
            File.Move($"{_mission.SqmPath}.txt", _mission.SqmPath);
        }
        else
        {
            throw new FileNotFoundException();
        }
    }

    private void Read()
    {
        _mission.SqmLines = File.ReadAllLines(_mission.SqmPath).Select(x => x.Trim()).ToList();
        _mission.SqmLines.RemoveAll(string.IsNullOrEmpty);
        RemoveUnbinText();
        ReadAllData();
        ReadSettings();
    }

    private void RemoveUnbinText()
    {
        if (_mission.SqmLines.First() != "////////////////////////////////////////////////////////////////////")
        {
            return;
        }

        _mission.SqmLines = _mission.SqmLines.Skip(7).ToList();
        // mission.sqmLines = mission.sqmLines.Take(mission.sqmLines.Count - 1).ToList();
    }

    private void ReadAllData()
    {
        Mission.NextId = Convert.ToInt32(MissionUtilities.ReadSingleDataByKey(MissionUtilities.ReadDataByKey(_mission.SqmLines, "ItemIDProvider"), "nextID"));
        _mission.RawEntities = MissionUtilities.ReadDataByKey(_mission.SqmLines, "Entities");
        _mission.MissionEntity = MissionEntityHelper.CreateFromItems(_mission.RawEntities);
    }

    private void ReadSettings()
    {
        _mission.MaxCurators = 5;
        var curatorsMaxLine = File.ReadAllLines(Path.Combine(_mission.Path, "cba_settings.sqf")).FirstOrDefault(x => x.Contains("uksf_curator_curatorsMax"));
        if (string.IsNullOrEmpty(curatorsMaxLine))
        {
            _mission.MaxCurators = _armaServerDefaultMaxCurators;
            _reports.Add(
                new ValidationReport(
                    "Using server setting 'uksf_curator_curatorsMax'",
                    "Could not find setting 'uksf_curator_curatorsMax' in cba_settings.sqf" +
                    "This is required to add the correct nubmer of pre-defined curator objects." +
                    $"The server setting value ({_mission.MaxCurators}) for this will be used instead."
                )
            );
            return;
        }

        var curatorsMaxString = curatorsMaxLine.Split("=")[1].RemoveSpaces().Replace(";", "");
        if (!int.TryParse(curatorsMaxString, out var maxCurators))
        {
            _reports.Add(
                new ValidationReport(
                    "Using hardcoded setting 'uksf_curator_curatorsMax'",
                    $"Could not read malformed setting: '{curatorsMaxLine}' in cba_settings.sqf" +
                    "This is required to add the correct nubmer of pre-defined curator objects." +
                    "The hardcoded value (5) will be used instead."
                )
            );
        }
        else
        {
            _mission.MaxCurators = maxCurators;
        }
    }

    private void Patch()
    {
        _mission.MissionEntity.Patch(_mission.MaxCurators);

        if (!MissionUtilities.CheckFlag(_mission, "missionImageIgnore"))
        {
            var imagePath = Path.Combine(_mission.Path, "uksf.paa");
            var modpackImagePath = Path.Combine(_armaServerModsPath, "@uksf", "UKSFTemplate.VR", "uksf.paa");
            if (File.Exists(modpackImagePath))
            {
                if (File.Exists(imagePath) && new FileInfo(imagePath).Length != new FileInfo(modpackImagePath).Length)
                {
                    _reports.Add(
                        new ValidationReport(
                            "Loading image was different",
                            "The mission loading image `uksf.paa` was found to be different from the default." +
                            "It has been replaced with the default UKSF image.\n\n" +
                            "If you wish this to be a custom image, see <a target=\"_blank\" href=https://github.com/uksf/modpack/wiki/SR5:-Mission-Patching#ignoring-custom-loading-image>this page</a> for details on how to configure this"
                        )
                    );
                }

                File.Copy(modpackImagePath, imagePath, true);
            }
        }
    }

    private void Write()
    {
        var start = MissionUtilities.GetIndexByKey(_mission.SqmLines, "Entities");
        var count = _mission.RawEntities.Count;
        _mission.SqmLines.RemoveRange(start, count);
        var newEntities = _mission.MissionEntity.Serialize();
        _mission.SqmLines.InsertRange(start, newEntities);
        _mission.SqmLines = _mission.SqmLines.Select(x => x.RemoveTrailingNewLineGroup().RemoveNewLines().RemoveEmbeddedQuotes()).ToList();
        File.WriteAllLines(_mission.SqmPath, _mission.SqmLines);
    }

    private void PatchDescription()
    {
        _mission.DescriptionLines = File.ReadAllLines(_mission.DescriptionPath).ToList();

        PatchMaxPlayers();
        CheckRequiredDescriptionItems();
        CheckConfigurableDescriptionItems();

        _mission.DescriptionLines = _mission.DescriptionLines.Where(x => !x.Contains("__EXEC")).ToList();

        File.WriteAllLines(_mission.DescriptionPath, _mission.DescriptionLines);
    }

    private void PatchMaxPlayers()
    {
        var playable = _mission.SqmLines.Select(x => x.RemoveSpaces()).Count(x => x.ContainsIgnoreCase("isPlayable=1") || x.ContainsIgnoreCase("isPlayer=1"));
        _mission.PlayerCount = playable;

        var maxPlayersIndex = _mission.DescriptionLines.FindIndex(x => x.ContainsIgnoreCase("maxPlayers"));
        if (maxPlayersIndex == -1)
        {
            _reports.Add(
                new ValidationReport(
                    "<i>maxPlayers</i>  in description.ext is missing",
                    "<i>maxPlayers</i>  in description.ext is missing or malformed\nThis item is required for the mission to be launched",
                    true
                )
            );
        }
        else
        {
            _mission.DescriptionLines[maxPlayersIndex] = $"    maxPlayers = {playable};";
        }
    }

    private void CheckConfigurableDescriptionItems()
    {
        CheckDescriptionItem("onLoadName", "\"UKSF: Operation\"", false);
        CheckDescriptionItem("onLoadMission", "\"UKSF: Operation\"", false);
        CheckDescriptionItem("overviewText", "\"UKSF: Operation\"", false);
    }

    private void CheckRequiredDescriptionItems()
    {
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
        CheckDescriptionItem("allowProfileGlasses", "0");
    }

    private void CheckDescriptionItem(string key, string defaultValue, bool required = true)
    {
        var index = _mission.DescriptionLines.FindIndex(x => x.Contains($"{key} = ") ||
                                                             x.Contains($"{key}=") ||
                                                             x.Contains($"{key}= ") ||
                                                             x.Contains($"{key} =")
        );
        if (index != -1)
        {
            var itemValue = _mission.DescriptionLines[index].Split("=")[1].Trim();
            itemValue = itemValue.Remove(itemValue.Length - 1);
            var equal = string.Equals(itemValue, defaultValue, StringComparison.InvariantCultureIgnoreCase);
            switch (equal)
            {
                case false when required:
                    _reports.Add(
                        new ValidationReport(
                            $"Required description.ext item <i>{key}</i>  value is not default",
                            $"<i>{key}</i>  in description.ext is '{itemValue}'\nThe default value is '{defaultValue}'\n\nYou should only change this if you know what you're doing"
                        )
                    ); break;
                case true when !required:
                    _reports.Add(
                        new ValidationReport(
                            $"Configurable description.ext item <i>{key}</i>  value is default",
                            $"<i>{key}</i>  in description.ext is the same as the default value '{itemValue}'\n\nThis should be changed based on your mission"
                        )
                    ); break;
            }

            return;
        }

        if (required)
        {
            _mission.DescriptionLines.Add($"{key} = {defaultValue};");
        }
        else
        {
            _reports.Add(
                new ValidationReport(
                    $"Configurable description.ext item <i>{key}</i>  is missing",
                    $"<i>{key}</i>  in description.ext is missing\nThis is required for the mission\n\n" +
                    "It is advised to copy the description.ext file directly from the template mission to your mission\nUKSFTemplate.VR is located in the modpack files",
                    true
                )
            );
        }
    }
}
