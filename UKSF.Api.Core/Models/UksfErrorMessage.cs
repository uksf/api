namespace UKSF.Api.Core.Models;

public class UksfErrorMessage
{
    public UksfErrorMessage(int statusCode, int detailCode, string error, ValidationReportDataset validation)
    {
        StatusCode = statusCode;
        DetailCode = detailCode;
        Error = error;
        Validation = validation;
    }

    public int DetailCode { get; set; }
    public string Error { get; set; }
    public int StatusCode { get; set; }
    public ValidationReportDataset Validation { get; set; }
}
