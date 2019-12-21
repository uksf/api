using System.Collections.Generic;

namespace UKSFWebsite.Api.Models.Documents {
    public enum DocumentPermissionType { ANY, RANK, UNIT, TRAINING, USER }

    public class DocumentPermissions {
        public HashSet<string> allowedTrainings = new HashSet<string>();
        public HashSet<string> allowedUnits = new HashSet<string>();
        public HashSet<string> allowedUsers = new HashSet<string>();
        public bool firstOr;
        public string minRank;
        public List<DocumentPermissionType> permissionTypes = new List<DocumentPermissionType>();
        public bool secondOr;
    }
}
