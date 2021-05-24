using System.Collections.Generic;

namespace UKSF.Api.Personnel.Models
{
    public class RecruitmentStatsDataset
    {
        public IEnumerable<RecruitmentActivityDataset> Activity;
        public RecruitmentStats Sr1Stats;
        public RecruitmentStats YourStats;
    }

    public class RecruitmentActivityDataset
    {
        public int Accepted;
        public object Account;
        public int Active;
        public string Name;
        public int Rejected;
    }

    public class RecruitmentStats
    {
        public IEnumerable<RecruitmentStat> LastMonth;
        public IEnumerable<RecruitmentStat> Overall;
    }

    public class RecruitmentStat
    {
        public string FieldName;
        public string FieldValue;
    }
}
