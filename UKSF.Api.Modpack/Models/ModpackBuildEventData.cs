using UKSF.Api.Core.Models;

namespace UKSF.Api.Modpack.Models;

public class ModpackBuildEventData(DomainModpackBuild build) : EventData
{
    public DomainModpackBuild Build { get; } = build;
}
