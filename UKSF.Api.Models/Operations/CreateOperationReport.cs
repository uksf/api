using System;

namespace UKSF.Api.Models.Operations {
    public class CreateOperationReportRequest {
        public string name, map, type, result;
        public DateTime start, end;
        public int starttime, endtime;
    }
}
