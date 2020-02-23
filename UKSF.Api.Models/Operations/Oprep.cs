using System;
using UKSF.Api.Models.Personnel;

namespace UKSF.Api.Models.Operations {
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
