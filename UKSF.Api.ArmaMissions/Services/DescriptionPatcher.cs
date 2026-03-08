using System.Text.RegularExpressions;
using UKSF.Api.ArmaMissions.Models;
using UKSF.Api.Core.Extensions;
using UKSF.Api.Core.Models;

namespace UKSF.Api.ArmaMissions.Services;

public interface IDescriptionPatcher
{
    void Patch(MissionPatchContext context);
}

public class DescriptionPatcher : IDescriptionPatcher
{
    public void Patch(MissionPatchContext context)
    {
        PatchMaxPlayers(context);
        CheckRequiredDescriptionItems(context);
        CheckConfigurableDescriptionItems(context);

        context.Description.Lines = context.Description.Lines.Where(x => !x.Contains("__EXEC")).ToList();
    }

    private static void PatchMaxPlayers(MissionPatchContext context)
    {
        var maxPlayersIndex = context.Description.Lines.FindIndex(x => x.ContainsIgnoreCase("maxPlayers"));
        if (maxPlayersIndex == -1)
        {
            context.Reports.Add(
                new ValidationReport(
                    "<i>maxPlayers</i>  in description.ext is missing",
                    "<i>maxPlayers</i>  in description.ext is missing or malformed\nThis item is required for the mission to be launched",
                    true
                )
            );
        }
        else
        {
            context.Description.Lines[maxPlayersIndex] = $"    maxPlayers = {context.PlayerCount};";
        }
    }

    private static void CheckConfigurableDescriptionItems(MissionPatchContext context)
    {
        CheckDescriptionItem(context, "onLoadName", "\"UKSF: Operation\"", false);
        CheckDescriptionItem(context, "onLoadMission", "\"UKSF: Operation\"", false);
        CheckDescriptionItem(context, "overviewText", "\"UKSF: Operation\"", false);
    }

    private static void CheckRequiredDescriptionItems(MissionPatchContext context)
    {
        CheckDescriptionItem(context, "author", "\"UKSF\"");
        CheckDescriptionItem(context, "loadScreen", "\"uksf.paa\"");
        CheckDescriptionItem(context, "respawn", "\"BASE\"");
        CheckDescriptionItem(context, "respawnOnStart", "1");
        CheckDescriptionItem(context, "respawnDelay", "1");
        CheckDescriptionItem(context, "respawnDialog", "0");
        CheckDescriptionItem(context, "respawnTemplates[]", "{ \"MenuPosition\" }");
        CheckDescriptionItem(context, "reviveMode", "0");
        CheckDescriptionItem(context, "disabledAI", "1");
        CheckDescriptionItem(context, "aiKills", "0");
        CheckDescriptionItem(context, "disableChannels[]", "{ 0,2,6 }");
        CheckDescriptionItem(context, "cba_settings_hasSettingsFile", "1");
        CheckDescriptionItem(context, "allowProfileGlasses", "0");
    }

    private static void CheckDescriptionItem(MissionPatchContext context, string key, string defaultValue, bool required = true)
    {
        var pattern = $@"^\s*{Regex.Escape(key)}\s*=";
        var index = context.Description.Lines.FindIndex(x => Regex.IsMatch(x, pattern));
        if (index != -1)
        {
            var itemValue = context.Description.Lines[index].Split("=")[1].Trim();
            itemValue = itemValue.Remove(itemValue.Length - 1);
            var equal = string.Equals(itemValue, defaultValue, StringComparison.InvariantCultureIgnoreCase);
            switch (equal)
            {
                case false when required:
                    context.Reports.Add(
                        new ValidationReport(
                            $"Required description.ext item <i>{key}</i>  value is not default",
                            $"<i>{key}</i>  in description.ext is '{itemValue}'\nThe default value is '{defaultValue}'\n\nYou should only change this if you know what you're doing"
                        )
                    ); break;
                case true when !required:
                    context.Reports.Add(
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
            context.Description.Lines.Add($"{key} = {defaultValue};");
        }
        else
        {
            context.Reports.Add(
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
