namespace UKSF.Api.Modpack.Models
{
    public class ModpackBuildStepEventData
    {
        public string BuildId;
        public ModpackBuildStep BuildStep;

        public ModpackBuildStepEventData(string buildId, ModpackBuildStep buildStep)
        {
            BuildId = buildId;
            BuildStep = buildStep;
        }
    }
}
