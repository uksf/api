namespace UKSF.Api.Shared.Models;

public class UksfErrorMessage
{
    public int DetailCode { get; set; }
    public string Error { get; set; }
    public int StatusCode { get; set; }
    public ValidationReportDataset Validation { get; set; }

    public UksfErrorMessage(int statusCode, int detailCode, string error, ValidationReportDataset validation)
    {
        StatusCode = statusCode;
        DetailCode = detailCode;
        Error = error;
        Validation = validation;
    }
}
