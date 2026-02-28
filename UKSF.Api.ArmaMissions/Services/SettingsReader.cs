using UKSF.Api.ArmaMissions.Models;
using UKSF.Api.Core.Extensions;
using UKSF.Api.Core.Models;

namespace UKSF.Api.ArmaMissions.Services;

public interface ISettingsReader
{
    void Read(MissionPatchContext context);
}

public class SettingsReader : ISettingsReader
{
    public void Read(MissionPatchContext context)
    {
        if (!File.Exists(context.CbaSettingsPath))
        {
            context.Reports.Add(
                new ValidationReport(
                    "Missing file: cba_settings.sqf",
                    "The mission is missing a required file:\ncba_settings.sqf\n\n" +
                    "It is advised to copy this file directly from the template mission to your mission and make changes according to the needs of the mission\n" +
                    "UKSFTemplate.VR is located in the modpack files",
                    true
                )
            );
            context.Aborted = true;
            return;
        }

        context.MaxCurators = 5;
        var curatorsMaxLine = File.ReadAllLines(context.CbaSettingsPath).FirstOrDefault(x => x.Contains("uksf_curator_curatorsMax"));
        if (string.IsNullOrEmpty(curatorsMaxLine))
        {
            context.MaxCurators = context.DefaultMaxCurators;
            context.Reports.Add(
                new ValidationReport(
                    "Using server setting 'uksf_curator_curatorsMax'",
                    "Could not find setting 'uksf_curator_curatorsMax' in cba_settings.sqf" +
                    "This is required to add the correct nubmer of pre-defined curator objects." +
                    $"The server setting value ({context.MaxCurators}) for this will be used instead."
                )
            );
            return;
        }

        var curatorsMaxString = curatorsMaxLine.Split("=")[1].RemoveSpaces().Replace(";", "");
        if (!int.TryParse(curatorsMaxString, out var maxCurators))
        {
            context.Reports.Add(
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
            context.MaxCurators = maxCurators;
        }
    }
}
