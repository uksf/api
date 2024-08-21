namespace UKSF.Api.Core.Models;

public class UksfErrorMessage(int statusCode, int detailCode, string error, ValidationReportDataset validation)
{
    public int DetailCode { get; set; } = detailCode;
    public string Error { get; set; } = error;
    public int StatusCode { get; set; } = statusCode;
    public ValidationReportDataset Validation { get; set; } = validation;
}
