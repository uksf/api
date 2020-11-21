using UKSF.Api.Base.Models;

namespace UKSF.Api.Launcher.Models {
    public record LauncherFile : MongoObject {
        public string FileName;
        public string Version;
    }
}
