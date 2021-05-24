using System.Collections.Generic;

namespace UKSF.Api.Teamspeak.Models
{
    public class TeampseakReportsDataset
    {
        public TeampseakReportDataset AcreData;
        public TeampseakReportDataset Data;
    }

    public class TeampseakReportDataset
    {
        public List<TeampseakReport> Datasets;
        public List<string> Labels;
    }

    public class TeampseakReport
    {
        public string BorderColor;
        public int[] Data;
        public bool Fill;
        public string Label;
    }
}
