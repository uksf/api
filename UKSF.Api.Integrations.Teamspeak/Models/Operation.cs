using System;
using UKSF.Api.Base.Models;
using UKSF.Api.Personnel.Models;

namespace UKSF.Api.Teamspeak.Models {
    public class Operation : MongoObject {
        public AttendanceReport AttendanceReport;
        public DateTime End;
        public string Map;
        public string Name;
        public string Result;
        public DateTime Start;
        public string Type;
    }
}
