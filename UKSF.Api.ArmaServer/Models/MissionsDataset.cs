using UKSF.Api.Core.Models;

namespace UKSF.Api.ArmaServer.Models;

public class MissionsDataset
{
    public List<MissionReportDataset> MissionReports { get; set; }
    public List<MissionFile> Missions { get; set; }
}

public class MissionReportDataset
{
    public string Mission { get; set; }
    public List<ValidationReport> Reports { get; set; }
}
