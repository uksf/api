namespace UKSF.Api.Shared.Models;

public class RecruitmentStatsDataset
{
    public IEnumerable<RecruitmentActivityDataset> Activity { get; set; }
    public RecruitmentStats Sr1Stats { get; set; }
    public RecruitmentStats YourStats { get; set; }
}

public class RecruitmentActivityDataset
{
    public int Accepted { get; set; }
    public object Account { get; set; }
    public int Active { get; set; }
    public string Name { get; set; }
    public int Rejected { get; set; }
}

public class RecruitmentStats
{
    public IEnumerable<RecruitmentStat> LastMonth { get; set; }
    public IEnumerable<RecruitmentStat> Overall { get; set; }
}

public class RecruitmentStat
{
    public string FieldName { get; set; }
    public string FieldValue { get; set; }
}
