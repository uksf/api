using System;

namespace UKSFWebsite.Api.Models.Requests {
    public class CreateOperationReportRequest {
        public string name, map, type, result;
        public DateTime start, end;
        public int starttime, endtime;
    }
}
