namespace UKSF.Api.Models.Personnel {
    public class ExtendedAccount : Account {
        public string displayName;
        public bool permissionRecruiter;
        public bool permissionRecruiterLead;
        public bool permissionServers;
        public bool permissionPersonnel;
        public bool permissionCommand;
        public bool permissionAdmin;
        public bool permissionNco;
    }
}
