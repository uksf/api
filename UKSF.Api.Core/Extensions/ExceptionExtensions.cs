namespace UKSF.Api.Core.Extensions;

public static class ExceptionExtensions
{
    public static string GetCompleteString(this Exception exception)
    {
        return exception.InnerException is not null ? $"{exception}\n{exception.InnerException}" : exception.ToString();
    }

    public static string GetCompleteMessage(this Exception exception)
    {
        return exception.InnerException is not null ? $"{exception.Message}\n{exception.InnerException.Message}" : exception.Message;
    }
}
