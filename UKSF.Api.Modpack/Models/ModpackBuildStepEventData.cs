using UKSF.Api.Core.Models;

namespace UKSF.Api.Modpack.Models;

public class ModpackBuildStepEventData(string buildId, ModpackBuildStep buildStep) : EventData
{
    public string BuildId { get; } = buildId;
    public ModpackBuildStep BuildStep { get; } = buildStep;
}
