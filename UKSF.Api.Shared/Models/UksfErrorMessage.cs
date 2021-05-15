namespace UKSF.Api.Shared.Models
{
    public class UksfErrorMessage
    {
        public string Error;

        public int StatusCode;

        public UksfErrorMessage(int statusCode, string error)
        {
            StatusCode = statusCode;
            Error = error;
        }
    }
}
