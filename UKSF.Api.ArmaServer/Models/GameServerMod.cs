namespace UKSF.Api.ArmaServer.Models {
    public class GameServerMod {
        public bool IsDuplicate;
        public string Name;
        public string Path;
        public string PathRelativeToServerExecutable;

        public override string ToString() => Name;
    }
}
