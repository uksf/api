using System;
using UKSF.Api.Base.Models;
using UKSF.Api.Personnel.Models;

namespace UKSF.Api.Command.Models {
    public class Oprep : DatabaseObject {
        public AttendanceReport attendanceReport;
        public string description;
        public DateTime end;
        public string map;
        public string name;
        public string result;
        public DateTime start;
        public string type;
    }
}
