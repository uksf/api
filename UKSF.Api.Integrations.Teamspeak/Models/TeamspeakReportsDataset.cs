namespace UKSF.Api.Integrations.Teamspeak.Models;

public class TeamspeakReportsDataset
{
    public TeamspeakReportDataset AcreData { get; set; }
    public TeamspeakReportDataset Data { get; set; }
}

public class TeamspeakReportDataset
{
    public List<TeamspeakReport> Datasets { get; set; }
    public List<string> Labels { get; set; }
}

public class TeamspeakReport
{
    public string BorderColor { get; set; }
    public int[] Data { get; set; }
    public bool Fill { get; set; }
    public string Label { get; set; }
}
