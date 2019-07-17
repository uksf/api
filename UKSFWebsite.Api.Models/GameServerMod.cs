namespace UKSFWebsite.Api.Models {
    public class GameServerMod {
        public bool isDuplicate;
        public string name;
        public string path;
        public string pathRelativeToServerExecutable;

        public override string ToString() => name;
    }
}
