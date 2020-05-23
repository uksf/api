using System;

namespace UKSF.Api.Models.Operations {
    public class Opord : DatabaseObject {
        public string description;
        public DateTime end;
        public string map;
        public string name;
        public DateTime start;
        public string type;
    }
}
