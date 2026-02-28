using UKSF.Api.ArmaMissions.Models;
using UKSF.Api.Core.Extensions;
using UKSF.Api.Core.Models;

namespace UKSF.Api.ArmaMissions.Services;

public interface IDescriptionReader
{
    void Read(MissionPatchContext context);
}

public class DescriptionReader : IDescriptionReader
{
    public void Read(MissionPatchContext context)
    {
        if (!File.Exists(context.DescriptionPath))
        {
            context.Reports.Add(
                new ValidationReport(
                    "Missing file: description.ext",
                    "The mission is missing a required file:\ndescription.ext\n\n" +
                    "It is advised to copy this file directly from the template mission to your mission\nUKSFTemplate.VR is located in the modpack files",
                    true
                )
            );
            context.Aborted = true;
            return;
        }

        var lines = File.ReadAllLines(context.DescriptionPath).ToList();
        context.Description = new DescriptionDocument
        {
            Lines = lines,
            MissionPatchingIgnore = lines.Any(x => x.ContainsIgnoreCase("missionPatchingIgnore")),
            UseSimplePack = lines.Any(x => x.ContainsIgnoreCase("missionUseSimplePack"))
        };
    }
}
