namespace UKSF.Api.Core.Extensions;

public static class ExceptionExtensions
{
    extension(Exception exception)
    {
        public string GetCompleteString()
        {
            return exception.InnerException is not null ? $"{exception}\n{exception.InnerException}" : exception.ToString();
        }

        public string GetCompleteMessage()
        {
            return exception.InnerException is not null ? $"{exception.Message}\n{exception.InnerException.Message}" : exception.Message;
        }
    }
}
