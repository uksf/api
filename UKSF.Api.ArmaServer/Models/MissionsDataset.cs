using System.Collections.Generic;
using UKSF.Api.Shared.Models;

namespace UKSF.Api.ArmaServer.Models
{
    public class MissionsDataset
    {
        public List<MissionReportDataset> MissionReports;
        public List<MissionFile> Missions;
    }

    public class MissionReportDataset
    {
        public string Mission;
        public List<ValidationReport> Reports;
    }
}
