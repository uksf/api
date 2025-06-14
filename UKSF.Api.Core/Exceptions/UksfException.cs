using UKSF.Api.Core.Models;

namespace UKSF.Api.Core.Exceptions;

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
