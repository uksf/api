namespace UKSF.Api.Core.Models;

public class ValidationReport
{
    public ValidationReport(Exception exception)
    {
        Title = exception.GetBaseException().Message;
        Detail = exception.ToString();
        Error = true;
    }

    public ValidationReport(string title, string detail, bool error = false)
    {
        Title = error ? $"Error: {title}" : $"Warning: {title}";
        Detail = detail;
        Error = error;
    }

    public string Detail { get; }
    public bool Error { get; }
    public string Title { get; }
}

public class ValidationReportDataset
{
    public List<ValidationReport> Reports { get; set; }
}
