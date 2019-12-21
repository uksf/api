using System;
using System.Collections.Generic;

namespace UKSFWebsite.Api.Models.Documents {
    public class DocumentVersion {
        public string author;
        public DateTime changed = DateTime.Now;
        public List<DocumentChange> changes = new List<DocumentChange>();
        public string versionFile;
        public int versionNumber;
    }
}
