using System;
using UKSF.Api.Base.Models;
using UKSF.Api.Personnel.Models;

namespace UKSF.Api.Command.Models {
    public record Oprep : MongoObject {
        public AttendanceReport AttendanceReport;
        public string Description;
        public DateTime End;
        public string Map;
        public string Name;
        public string Result;
        public DateTime Start;
        public string Type;
    }
}
