namespace UKSF.Api.Integrations.Teamspeak.Models;

public class TeampseakReportsDataset
{
    public TeampseakReportDataset AcreData { get; set; }
    public TeampseakReportDataset Data { get; set; }
}

public class TeampseakReportDataset
{
    public List<TeampseakReport> Datasets { get; set; }
    public List<string> Labels { get; set; }
}

public class TeampseakReport
{
    public string BorderColor { get; set; }
    public int[] Data { get; set; }
    public bool Fill { get; set; }
    public string Label { get; set; }
}
