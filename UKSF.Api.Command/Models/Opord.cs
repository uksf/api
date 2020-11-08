using System;
using UKSF.Api.Base.Models;

namespace UKSF.Api.Command.Models {
    public class Opord : DatabaseObject {
        public string description;
        public DateTime end;
        public string map;
        public string name;
        public DateTime start;
        public string type;
    }
}
