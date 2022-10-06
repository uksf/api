using UKSF.Api.Shared.Models;

namespace UKSF.Api.Shared.Exceptions;

[Serializable]
public class UksfException : Exception
{
    public UksfException(string message, int statusCode, int detailCode = 0, ValidationReportDataset validation = null) : base(message)
    {
        StatusCode = statusCode;
        DetailCode = detailCode;
        Validation = validation;
    }

    public int StatusCode { get; }
    public int DetailCode { get; }
    public ValidationReportDataset Validation { get; }
}
