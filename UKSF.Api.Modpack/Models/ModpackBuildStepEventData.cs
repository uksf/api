namespace UKSF.Api.Modpack.Models {
    public class ModpackBuildStepEventData {
        public ModpackBuildStepEventData(string buildId, ModpackBuildStep buildStep) {
            BuildId = buildId;
            BuildStep = buildStep;
        }

        public string BuildId { get; }
        public ModpackBuildStep BuildStep { get; }
    }
}
