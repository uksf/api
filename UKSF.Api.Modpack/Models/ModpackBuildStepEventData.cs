namespace UKSF.Api.Modpack.Models;

public class ModpackBuildStepEventData
{
    public string BuildId { get; set; }
    public ModpackBuildStep BuildStep { get; set; }

    public ModpackBuildStepEventData(string buildId, ModpackBuildStep buildStep)
    {
        BuildId = buildId;
        BuildStep = buildStep;
    }
}
