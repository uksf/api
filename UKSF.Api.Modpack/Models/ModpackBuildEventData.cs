using UKSF.Api.Core.Models;

namespace UKSF.Api.Modpack.Models;

public class ModpackBuildEventData(ModpackBuild build) : EventData
{
    public ModpackBuild Build { get; } = build;
}
