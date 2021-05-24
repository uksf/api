namespace UKSF.Api.Shared.Models
{
    public class UksfErrorMessage
    {
        public int DetailCode;
        public string Error;
        public int StatusCode;
        public ValidationReportDataset Validation;

        public UksfErrorMessage(int statusCode, int detailCode, string error, ValidationReportDataset validation)
        {
            StatusCode = statusCode;
            DetailCode = detailCode;
            Error = error;
            Validation = validation;
        }
    }
}
