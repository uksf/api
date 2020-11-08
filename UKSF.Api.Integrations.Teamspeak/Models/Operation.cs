using System;
using UKSF.Api.Base.Models;
using UKSF.Api.Personnel.Models;

namespace UKSF.Api.Teamspeak.Models {
    public class Operation : DatabaseObject {
        public AttendanceReport attendanceReport;
        public DateTime end;
        public string map;
        public string name;
        public string result;
        public DateTime start;
        public string type;
    }
}
