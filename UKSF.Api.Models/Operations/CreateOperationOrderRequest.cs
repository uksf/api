using System;

namespace UKSF.Api.Models.Operations {
    public class CreateOperationOrderRequest {
        public string name, map, type;
        public DateTime start, end;
        public int starttime, endtime;
    }
}
