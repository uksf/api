using System;
using UKSF.Api.Base.Models;

namespace UKSF.Api.Command.Models {
    public class Opord : DatabaseObject {
        public string Description;
        public DateTime End;
        public string Map;
        public string Name;
        public DateTime Start;
        public string Type;
    }
}
