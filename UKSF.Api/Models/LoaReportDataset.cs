using System;
using System.Collections.Generic;
using UKSF.Api.Personnel.Models;

namespace UKSF.Api.Models
{
    [Serializable]
    public class LoaReportDataset
    {
        public List<LoaReport> ActiveLoas;
        public List<LoaReport> PastLoas;
        public List<LoaReport> UpcomingLoas;
    }

    public class LoaReport
    {
        public bool Emergency;
        public DateTime End;
        public string Id;
        public bool InChainOfCommand;
        public bool Late;
        public bool LongTerm;
        public string Name;
        public string Reason;
        public DateTime Start;
        public LoaReviewState State;
    }
}
